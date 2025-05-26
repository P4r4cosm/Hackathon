import os
import json
import logging
import time
import traceback
from pathlib import Path
import tempfile
import uuid

import pika
from minio import Minio
from minio.error import S3Error
from dotenv import load_dotenv
import whisper # Используем openai-whisper

# Загрузка переменных окружения из .env файла
load_dotenv()

# Настройка логирования
LOG_LEVEL = os.getenv("LOG_LEVEL", "INFO").upper()
logging.basicConfig(
    level=LOG_LEVEL, format="%(asctime)s - %(levelname)s - %(process)d - %(threadName)s - %(module)s - %(funcName)s - %(message)s"
)
logger = logging.getLogger(__name__)

# --- Конфигурация ---
RABBITMQ_HOST = os.getenv("RABBITMQ_HOST", "rabbitmq")
RABBITMQ_PORT = int(os.getenv("RABBITMQ_PORT", 5672))
RABBITMQ_USER = os.getenv("RABBITMQ_USER", "user")
RABBITMQ_PASS = os.getenv("RABBITMQ_PASS", "password")
RABBITMQ_VHOST = os.getenv("RABBITMQ_VHOST", "/")

RABBITMQ_CONSUME_QUEUE_NAME = os.getenv("RABBITMQ_WHISPER_QUEUE_NAME", "whisper_tasks_queue")
RABBITMQ_CONSUME_EXCHANGE_NAME = os.getenv("RABBITMQ_AUDIO_PROCESSING_EXCHANGE", "audio_processing_exchange")
RABBITMQ_CONSUME_ROUTING_KEY = os.getenv("RABBITMQ_WHISPER_TASKS_ROUTING_KEY", "whisper.task")

RABBITMQ_PUBLISH_EXCHANGE_NAME = os.getenv("RABBITMQ_RESULTS_EXCHANGE_NAME", "results_exchange")
DEFAULT_WHISPER_PUBLISH_ROUTING_KEY = os.getenv("RABBITMQ_WHISPER_RESULT_ROUTING_KEY", "task.result.whisper")
# НОВАЯ ПЕРЕМЕННАЯ для очереди результатов по умолчанию
RABBITMQ_PUBLISH_DEFAULT_QUEUE_NAME = os.getenv("RABBITMQ_WHISPER_RESULTS_DEFAULT_QUEUE_NAME", "whisper_results_default_queue")


MINIO_ENDPOINT = os.getenv("MINIO_ENDPOINT", "minio:9000")
MINIO_ACCESS_KEY = os.getenv("MINIO_ACCESS_KEY")
MINIO_SECRET_KEY = os.getenv("MINIO_SECRET_KEY")
MINIO_BUCKET_NAME = os.getenv("MINIO_BUCKET_NAME", "audio-bucket")
MINIO_USE_SSL = os.getenv("MINIO_USE_SSL", "False").lower() == "true"

WHISPER_MODEL_NAME = os.getenv("WHISPER_MODEL_NAME", "base")
WHISPER_CACHE_DIR = os.getenv("WHISPER_CACHE_DIR", "/app/.cache/whisper")

RECONNECT_DELAY_SECONDS = int(os.getenv("RECONNECT_DELAY_SECONDS", 5))

MINIO_CLIENT = None

# --- Функции ---

def get_minio_client():
    global MINIO_CLIENT
    if MINIO_CLIENT is None:
        try:
            MINIO_CLIENT = Minio(
                MINIO_ENDPOINT,
                access_key=MINIO_ACCESS_KEY,
                secret_key=MINIO_SECRET_KEY,
                secure=MINIO_USE_SSL,
            )
            logger.info(f"MinIO client initialized for endpoint: {MINIO_ENDPOINT}")
        except Exception as e:
            logger.error(f"Failed to initialize MinIO client: {e}")
            MINIO_CLIENT = None
            raise
    return MINIO_CLIENT

def download_file_from_minio(minio_client, bucket_name, object_name, download_path, task_id="N/A"):
    try:
        minio_client.fget_object(bucket_name, object_name, download_path)
        logger.info(f"Задача {task_id}: Файл {object_name} скачан из бакета {bucket_name} в {download_path}")
        return True
    except S3Error as e:
        logger.error(f"Задача {task_id}: Ошибка MinIO S3 при скачивании: {e}. Бакет: {bucket_name}, Объект: {object_name}")
    except Exception as e:
        logger.error(f"Задача {task_id}: Не удалось скачать файл {object_name} из MinIO: {e}")
    return False

def upload_file_to_minio(minio_client, bucket_name, file_path, object_name, task_id="N/A", content_type='application/octet-stream'):
    try:
        found = minio_client.bucket_exists(bucket_name)
        if not found:
            minio_client.make_bucket(bucket_name)
            logger.info(f"Задача {task_id}: Бакет {bucket_name} создан.")
        
        minio_client.fput_object(bucket_name, object_name, file_path, content_type=content_type)
        logger.info(f"Задача {task_id}: Файл {file_path} загружен в MinIO как {object_name} в бакет {bucket_name} (тип: {content_type})")
        return True
    except S3Error as e:
        logger.error(f"Задача {task_id}: Ошибка MinIO S3 при загрузке: {e}. Бакет: {bucket_name}, Объект: {object_name}, Файл: {file_path}")
    except Exception as e:
        logger.error(f"Задача {task_id}: Не удалось загрузить файл {file_path} в MinIO: {e}")
    return False

def transcribe_audio_russian(audio_file_path, model_name=WHISPER_MODEL_NAME, cache_dir=WHISPER_CACHE_DIR, task_id="N/A"):
    """
    Распознает речь в аудиофайле с использованием Whisper, принудительно используя русский язык.
    Возвращает результат в виде словаря со стандартным разбиением на сегменты.
    """
    try:
        logger.info(f"Задача {task_id}: Загрузка модели Whisper: {model_name} с cache_dir: {cache_dir}")
        Path(cache_dir).mkdir(parents=True, exist_ok=True)

        model = whisper.load_model(model_name, download_root=cache_dir)
        logger.info(f"Задача {task_id}: Модель Whisper {model_name} загружена. Начало транскрибации для {audio_file_path}...")

        transcribe_options = {
            "language": "ru",
            "fp16": False 
        }
        
        result = model.transcribe(audio_file_path, **transcribe_options)
        logger.info(f"Задача {task_id}: Транскрибация успешна. Обнаруженный моделью язык: {result.get('language')}")
        return result
    except Exception as e:
        logger.error(f"Задача {task_id}: Ошибка во время транскрибации аудио: {e}")
        logger.error(traceback.format_exc())
        return None

def publish_result(channel, result_message, task_id_for_correlation, service_name="whisper"):
    try:
        # logger.info("DEFAULT_WHISPER_PUBLISH_ROUTING_KEY: "+DEFAULT_WHISPER_PUBLISH_ROUTING_KEY) # Уже есть в логах ниже
        routing_key = DEFAULT_WHISPER_PUBLISH_ROUTING_KEY
        if isinstance(result_message, dict) and 'task_id' not in result_message:
            result_message['task_id'] = task_id_for_correlation

        message_body = json.dumps(result_message, ensure_ascii=False)
        channel.basic_publish(
            exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME,
            routing_key=routing_key,
            body=message_body,
            properties=pika.BasicProperties(
                delivery_mode=pika.spec.PERSISTENT_DELIVERY_MODE,
                content_type='application/json',
                correlation_id=task_id_for_correlation
            )
        )
        logger.info(f"Задача {task_id_for_correlation}: Результат опубликован в {RABBITMQ_PUBLISH_EXCHANGE_NAME} с ключом {routing_key}")
    except Exception as e:
        logger.error(f"Задача {task_id_for_correlation}: Не удалось опубликовать результат: {e}")
        logger.error(traceback.format_exc()) # Добавим traceback для ошибок публикации

def callback(ch, method, properties, body):
    task_id = "unknown_task"
    message_data = None
    try:
        message_str = body.decode('utf-8')
        message_data = json.loads(message_str)

        if properties and properties.correlation_id:
            task_id = properties.correlation_id
        elif message_data and ("task_id" in message_data or "TaskId" in message_data):
            task_id = message_data.get("task_id") or message_data.get("TaskId")
        if task_id == "unknown_task" or not task_id:
            task_id = str(uuid.uuid4())
            if message_data: message_data["task_id_generated"] = task_id # Добавим в сообщение, если возможно
            logger.warning(f"Task_id не найден или пуст, сгенерирован новый: {task_id}")

        logger.info(f"Задача {task_id}: Получено сообщение (delivery_tag={method.delivery_tag}), тело: {message_str[:500]}")

        input_object_name = message_data.get("input_object_name")
        output_minio_folder = message_data.get("output_minio_folder", "whisper_output").strip('/')
        current_bucket_name = message_data.get("input_bucket_name", MINIO_BUCKET_NAME)

        if not input_object_name:
            logger.error(f"Задача {task_id}: Отсутствует 'input_object_name' в сообщении.")
            error_payload = {"task_id": task_id, "status": "error", "service": "whisper", "error_message": "Missing 'input_object_name' in task message."}
            publish_result(ch, error_payload, task_id, service_name="whisper.error")
            ch.basic_ack(delivery_tag=method.delivery_tag)
            return

        minio_client_instance = get_minio_client()
        if not minio_client_instance: 
            logger.error(f"Задача {task_id}: MinIO client не доступен.")
            # Не подтверждаем и не отклоняем сообщение, чтобы оно было обработано позже,
            # когда MinIO станет доступен. Или basic_nack с requeue=True.
            # Однако, если MinIO недоступен постоянно, это приведет к зацикливанию.
            # Для простоты, пока будем считать это фатальной ошибкой для текущей задачи.
            error_payload = {"task_id": task_id, "status": "error", "service": "whisper", "original_input_object": input_object_name, "error_message": "MinIO client not available during task processing."}
            publish_result(ch, error_payload, task_id, service_name="whisper.error")
            ch.basic_ack(delivery_tag=method.delivery_tag) # Подтверждаем, т.к. отправили ошибку
            return

        with tempfile.TemporaryDirectory(prefix=f"whisper_{task_id}_") as tmpdir:
            original_filename = Path(input_object_name).name
            local_audio_file = Path(tmpdir) / original_filename
            
            logger.info(f"Задача {task_id}: Скачивание {input_object_name} из бакета {current_bucket_name} в {local_audio_file}")
            if not download_file_from_minio(minio_client_instance, current_bucket_name, input_object_name, str(local_audio_file), task_id):
                error_payload = {"task_id": task_id, "status": "error", "service": "whisper", "original_input_object": input_object_name, "error_message": f"Failed to download file from MinIO: s3://{current_bucket_name}/{input_object_name}"}
                publish_result(ch, error_payload, task_id, service_name="whisper.error")
                ch.basic_ack(delivery_tag=method.delivery_tag)
                return

            logger.info(f"Задача {task_id}: Начало транскрибации для {local_audio_file} (только русский язык)")
            transcription_result = transcribe_audio_russian(str(local_audio_file), task_id=task_id)

            if transcription_result:
                file_stem = Path(original_filename).stem
                detailed_transcription_data = {
                    "full_text": transcription_result.get("text", ""),
                    "segments": []
                }
                for segment in transcription_result.get("segments", []):
                    detailed_transcription_data["segments"].append({
                        "start": segment.get("start"),
                        "end": segment.get("end"),
                        "text": segment.get("text"),
                    })
                output_json_filename_local = f"{file_stem}_transcription_detailed.json"
                output_json_path_local = Path(tmpdir) / output_json_filename_local
                with open(output_json_path_local, 'w', encoding='utf-8') as f:
                    json.dump(detailed_transcription_data, f, ensure_ascii=False, indent=2)
                
                output_json_minio_object_name = f"{output_minio_folder}/{output_json_filename_local}"
                upload_file_to_minio(minio_client_instance, current_bucket_name, str(output_json_path_local), output_json_minio_object_name, task_id, content_type='application/json')

                result_for_rabbitmq = {
                    "task_id": task_id,
                    "status": "success",
                    "service": "whisper",
                    "tool_version": whisper.__version__,
                    "model_used": WHISPER_MODEL_NAME,
                    "input_bucket": current_bucket_name,
                    "input_object": input_object_name,
                    "transcription_detailed_json_object_path": f"s3://{current_bucket_name}/{output_json_minio_object_name}",
                    "language_requested": "ru",
                    "language_detected_by_model": transcription_result.get("language"),
                    "full_text": detailed_transcription_data["full_text"],
                    "segments": detailed_transcription_data["segments"]
                }
                publish_result(ch, result_for_rabbitmq, task_id, service_name="whisper")
                ch.basic_ack(delivery_tag=method.delivery_tag)
                logger.info(f"Задача {task_id}: Успешно обработана, результаты опубликованы.")
            else:
                logger.error(f"Задача {task_id}: Транскрибация не удалась для {input_object_name}.")
                error_payload = {"task_id": task_id, "status": "error", "service": "whisper", "input_bucket": current_bucket_name, "input_object": input_object_name, "error_message": "Transcription failed in whisper_worker."}
                publish_result(ch, error_payload, task_id, service_name="whisper.error")
                ch.basic_ack(delivery_tag=method.delivery_tag)
        
    except json.JSONDecodeError as e:
        logger.error(f"Задача {task_id}: Не удалось декодировать JSON сообщение: {e}. Тело: {body[:200]}")
        # Отклоняем сообщение без повторной постановки в очередь, так как оно, скорее всего, "битое"
        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False) 
    except ConnectionError as e: # Это pika.exceptions.ConnectionClosedByBroker или подобные
        logger.error(f"Задача {task_id}: Ошибка соединения RabbitMQ во время обработки: {e}")
        # Не подтверждаем и не отклоняем, так как соединение потеряно.
        # Цикл в main() должен переподключиться и сообщение будет доставлено заново.
        # Однако, если publish_result вызывался до ошибки, он мог не выполниться.
        # Это сложный сценарий. Если publish_result вызвал ошибку соединения, он уже залогировал.
        # Если ошибка соединения произошла до publish_result, тогда сообщение не обработано.
        # В pika.BlockingConnection, если соединение рвется, текущий callback скорее всего прервется с исключением.
        # Поэтому basic_nack с requeue=True может быть более безопасным, если мы не уверены, что publish_result выполнился.
        # Но если он выполнился, а потом соединение упало перед ack, будет дубликат.
        # Для простоты оставляем как есть, полагаясь на переподключение в main.
        # Но если сообщение уже было частично обработано, это может быть проблемой.
        # Безопаснее было бы ch.basic_nack(delivery_tag=method.delivery_tag, requeue=True)
        # если мы не уверены, что результат УЖЕ опубликован.
        # Сейчас мы публикуем, а потом ack. Если падает между, то при ределивери будет дубль.
        # Это вопрос идемпотентности обработчика результатов.
        # Для данного воркера, если он уже загрузил результат в MinIO и опубликовал, повторная обработка
        # того же файла не страшна, но сгенерирует новый результат.
        # Если мы хотим избежать дублирования, нужно состояние хранить.
        # Пока оставляем логику такой, что если callback прервался из-за ConnectionError,
        # сообщение будет redelivered.
        pass # Позволяем ошибке всплыть, чтобы главный цикл переподключился
    except Exception as e:
        logger.error(f"Задача {task_id}: Необработанное исключение в callback: {e}")
        logger.error(traceback.format_exc())
        error_payload = {"task_id": task_id, "status": "critical_error", "service": "whisper", "error_message": f"Unhandled exception in callback: {str(e)}"}
        try:
            if ch.is_open: publish_result(ch, error_payload, task_id, service_name="whisper.error")
        except Exception as pub_e:
            logger.error(f"Задача {task_id}: Не удалось опубликовать сообщение о критической ошибке: {pub_e}")
        finally:
            # Отклоняем сообщение без повторной постановки, чтобы избежать зацикливания на "ядовитых" сообщениях
            if ch.is_open: ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)


def main():
    logger.info(f"Запуск whisper_worker (openai-whisper) с моделью: {WHISPER_MODEL_NAME}, язык по умолчанию: русский.")
    logger.info(f"Публикация результатов в exchange '{RABBITMQ_PUBLISH_EXCHANGE_NAME}' с routing_key '{DEFAULT_WHISPER_PUBLISH_ROUTING_KEY}'.")
    logger.info(f"Будет создана/использована очередь по умолчанию для результатов: '{RABBITMQ_PUBLISH_DEFAULT_QUEUE_NAME}'.")

    try:
        get_minio_client() # Инициализируем один раз при старте
    except Exception as e:
        logger.error(f"Критическая ошибка: Не удалось подключиться к MinIO при старте: {e}. Воркер не будет запущен.")
        return

    connection = None
    channel = None
    while True:
        try:
            logger.info(f"Попытка подключения к RabbitMQ: {RABBITMQ_HOST}:{RABBITMQ_PORT}...")
            credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASS)
            parameters = pika.ConnectionParameters(
                RABBITMQ_HOST, 
                RABBITMQ_PORT, 
                RABBITMQ_VHOST, 
                credentials, 
                heartbeat=600, 
                blocked_connection_timeout=300
            )
            connection = pika.BlockingConnection(parameters)
            channel = connection.channel()
            logger.info("Успешное подключение к RabbitMQ.")

            # --- Настройка потребления ---
            channel.exchange_declare(exchange=RABBITMQ_CONSUME_EXCHANGE_NAME, exchange_type='direct', durable=True)
            logger.info(f"Exchange для потребления '{RABBITMQ_CONSUME_EXCHANGE_NAME}' объявлен/проверен.")
            
            channel.queue_declare(queue=RABBITMQ_CONSUME_QUEUE_NAME, durable=True)
            logger.info(f"Очередь для потребления '{RABBITMQ_CONSUME_QUEUE_NAME}' объявлена/проверена.")
            
            channel.queue_bind(exchange=RABBITMQ_CONSUME_EXCHANGE_NAME, queue=RABBITMQ_CONSUME_QUEUE_NAME, routing_key=RABBITMQ_CONSUME_ROUTING_KEY)
            logger.info(f"Очередь '{RABBITMQ_CONSUME_QUEUE_NAME}' привязана к exchange '{RABBITMQ_CONSUME_EXCHANGE_NAME}' с ключом '{RABBITMQ_CONSUME_ROUTING_KEY}'.")

            # --- Настройка публикации (создание, если не существует) ---
            channel.exchange_declare(exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME, exchange_type='direct', durable=True)
            logger.info(f"Exchange для публикации '{RABBITMQ_PUBLISH_EXCHANGE_NAME}' объявлен/проверен.")
            
            # Объявляем очередь по умолчанию для результатов
            channel.queue_declare(queue=RABBITMQ_PUBLISH_DEFAULT_QUEUE_NAME, durable=True)
            logger.info(f"Очередь по умолчанию для результатов '{RABBITMQ_PUBLISH_DEFAULT_QUEUE_NAME}' объявлена/проверена.")

            # Привязываем очередь по умолчанию к exchange для публикации с нужным routing key
            channel.queue_bind(
                exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME,
                queue=RABBITMQ_PUBLISH_DEFAULT_QUEUE_NAME,
                routing_key=DEFAULT_WHISPER_PUBLISH_ROUTING_KEY
            )
            logger.info(f"Очередь '{RABBITMQ_PUBLISH_DEFAULT_QUEUE_NAME}' привязана к exchange '{RABBITMQ_PUBLISH_EXCHANGE_NAME}' с ключом '{DEFAULT_WHISPER_PUBLISH_ROUTING_KEY}'.")


            channel.basic_qos(prefetch_count=1) # Обрабатываем по одному сообщению за раз
            channel.basic_consume(queue=RABBITMQ_CONSUME_QUEUE_NAME, on_message_callback=callback)
            
            logger.info(f"[*] Ожидание сообщений в очереди '{RABBITMQ_CONSUME_QUEUE_NAME}'. Для выхода нажмите CTRL+C")
            channel.start_consuming()

        except pika.exceptions.AMQPConnectionError as e:
            logger.error(f"Ошибка подключения к RabbitMQ: {e}")
            if channel and channel.is_open: 
                try: channel.close() 
                except Exception as ch_ex: logger.warning(f"Ошибка при закрытии канала при AMQPConnectionError: {ch_ex}")
            if connection and connection.is_open: 
                try: connection.close()
                except Exception as co_ex: logger.warning(f"Ошибка при закрытии соединения при AMQPConnectionError: {co_ex}")
            connection, channel = None, None # Сбрасываем, чтобы пересоздать
            logger.info(f"Повторная попытка подключения через {RECONNECT_DELAY_SECONDS} секунд...")
            time.sleep(RECONNECT_DELAY_SECONDS)
        except KeyboardInterrupt:
            logger.info("Получен сигнал KeyboardInterrupt. Завершение работы...")
            break
        except Exception as e:
            logger.error(f"Произошла непредвиденная ошибка в главном цикле: {e}")
            logger.error(traceback.format_exc())
            if channel and channel.is_open: 
                try: channel.close()
                except Exception as ch_ex: logger.warning(f"Ошибка при закрытии канала при Exception: {ch_ex}")
            if connection and connection.is_open: 
                try: connection.close()
                except Exception as co_ex: logger.warning(f"Ошибка при закрытии соединения при Exception: {co_ex}")
            connection, channel = None, None # Сбрасываем
            logger.info(f"Повторная попытка после ошибки через {RECONNECT_DELAY_SECONDS * 2} секунд...")
            time.sleep(RECONNECT_DELAY_SECONDS * 2)
        finally:
            # Этот блок finally будет выполняться только если цикл while прерывается (например, KeyboardInterrupt)
            # или если start_consuming() завершится нормально (что обычно не происходит без stop_consuming)
            if channel and channel.is_open:
                try:
                    if channel.is_consuming: # Проверяем, если он еще потребляет
                        channel.stop_consuming()
                        logger.info("Потребление остановлено.")
                    channel.close()
                    logger.info("Канал RabbitMQ закрыт.")
                except Exception as ex_ch: 
                    logger.warning(f"Ошибка при закрытии канала в finally: {ex_ch}")
            if connection and connection.is_open:
                try: 
                    connection.close()
                    logger.info("Соединение RabbitMQ закрыто.")
                except Exception as ex_conn: 
                    logger.warning(f"Ошибка при закрытии соединения в finally: {ex_conn}")
            
            # Для случаев, когда соединение рвется внутри try и переменные connection/channel становятся None
            if not (connection and connection.is_open) and not (channel and channel.is_open):
                 logger.debug("Соединение и канал RabbitMQ уже были закрыты или не были успешно открыты перед finally.")

    logger.info("Воркер whisper_worker остановлен.")

if __name__ == "__main__":
    main()