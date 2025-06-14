import os
import sys
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
    level=LOG_LEVEL, format="%(asctime)s - %(levelname)s - %(process)d - %(module)s - %(funcName)s - %(message)s"
)
logger = logging.getLogger(__name__)

# --- Универсальная конфигурация RabbitMQ ---
RABBITMQ_HOST = os.getenv("RABBITMQ_HOST", "rabbitmq")
RABBITMQ_PORT = int(os.getenv("RABBITMQ_PORT", 5672))
RABBITMQ_USER = os.getenv("RABBITMQ_USER", "user")
RABBITMQ_PASS = os.getenv("RABBITMQ_PASS", "password")
RABBITMQ_VHOST = os.getenv("RABBITMQ_VHOST", "/")

CONSUME_QUEUE = os.getenv('RABBITMQ_CONSUME_QUEUE')
CONSUME_EXCHANGE = os.getenv('RABBITMQ_CONSUME_EXCHANGE')
CONSUME_ROUTING_KEY = os.getenv('RABBITMQ_CONSUME_ROUTING_KEY')

PUBLISH_EXCHANGE = os.getenv('RABBITMQ_PUBLISH_EXCHANGE')
PUBLISH_ROUTING_KEY = os.getenv('RABBITMQ_PUBLISH_ROUTING_KEY')

# --- Проверка обязательных переменных RabbitMQ ---
required_vars = [
    'CONSUME_QUEUE', 'CONSUME_EXCHANGE', 'CONSUME_ROUTING_KEY',
    'PUBLISH_EXCHANGE', 'PUBLISH_ROUTING_KEY'
]
missing_vars = [var for var in required_vars if not globals()[var]]
if missing_vars:
    logger.critical(f"ОШИБКА: Отсутствуют обязательные переменные окружения RabbitMQ: {', '.join(missing_vars)}")
    sys.exit(1)

# --- Остальная конфигурация ---
MINIO_ENDPOINT = os.getenv("MINIO_ENDPOINT", "minio:9000")
MINIO_ACCESS_KEY = os.getenv("MINIO_ACCESS_KEY")
MINIO_SECRET_KEY = os.getenv("MINIO_SECRET_KEY")
MINIO_BUCKET_NAME = os.getenv("MINIO_BUCKET_NAME", "audio-bucket")
MINIO_USE_SSL = os.getenv("MINIO_USE_SSL", "False").lower() == "true"

WHISPER_MODEL_NAME = os.getenv("WHISPER_MODEL_NAME", "base")
WHISPER_CACHE_DIR = os.getenv("WHISPER_CACHE_DIR", "/app/.cache/whisper")

RECONNECT_DELAY_SECONDS = int(os.getenv("RECONNECT_DELAY_SECONDS", 5))

MINIO_CLIENT = None

# --- Функции (без изменений, кроме publish_result) ---

def get_minio_client():
    # ... (код без изменений)
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
    # ... (код без изменений)
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
    # ... (код без изменений)
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
    # ... (код без изменений)
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

# ИСПРАВЛЕНО: функция publish_result стала универсальной
def publish_result(channel, result_message, task_id_for_correlation):
    try:
        if isinstance(result_message, dict) and 'task_id' not in result_message:
            result_message['task_id'] = task_id_for_correlation

        message_body = json.dumps(result_message, ensure_ascii=False)
        channel.basic_publish(
            exchange=PUBLISH_EXCHANGE,
            routing_key=PUBLISH_ROUTING_KEY,
            body=message_body,
            properties=pika.BasicProperties(
                delivery_mode=pika.spec.PERSISTENT_DELIVERY_MODE,
                content_type='application/json',
                correlation_id=task_id_for_correlation
            )
        )
        logger.info(f"Задача {task_id_for_correlation}: Результат опубликован в '{PUBLISH_EXCHANGE}' с ключом '{PUBLISH_ROUTING_KEY}'")
    except Exception as e:
        logger.error(f"Задача {task_id_for_correlation}: Не удалось опубликовать результат: {e}")
        logger.error(traceback.format_exc())

# ИСПРАВЛЕНО: callback теперь использует универсальную функцию publish_result
def callback(ch, method, properties, body):
    # ... (код почти без изменений, только вызовы publish_result)
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
        original_input_object = message_data.get("original_input_object") or input_object_name
        current_bucket_name = message_data.get("input_bucket_name", MINIO_BUCKET_NAME)

        if not input_object_name:
            logger.error(f"Задача {task_id}: Отсутствует 'input_object_name' в сообщении.")
            error_payload = {"task_id": task_id, "status": "error", "service": "whisper", "error_message": "Missing 'input_object_name' in task message."}
            publish_result(ch, error_payload, task_id)
            ch.basic_ack(delivery_tag=method.delivery_tag)
            return

        minio_client_instance = get_minio_client()
        if not minio_client_instance: 
            error_payload = {"task_id": task_id, "status": "error", "service": "whisper", "original_input_object": input_object_name, "error_message": "MinIO client not available during task processing."}
            publish_result(ch, error_payload, task_id)
            ch.basic_ack(delivery_tag=method.delivery_tag) # Подтверждаем, т.к. отправили ошибку
            return

        with tempfile.TemporaryDirectory(prefix=f"whisper_{task_id}_") as tmpdir:
            original_filename = Path(input_object_name).name
            local_audio_file = Path(tmpdir) / original_filename
            
            logger.info(f"Задача {task_id}: Скачивание {input_object_name} из бакета {current_bucket_name} в {local_audio_file}")
            if not download_file_from_minio(minio_client_instance, current_bucket_name, input_object_name, str(local_audio_file), task_id):
                error_payload = {"task_id": task_id, "status": "error", "service": "whisper", "original_input_object": input_object_name, "error_message": f"Failed to download file from MinIO: s3://{current_bucket_name}/{input_object_name}"}
                publish_result(ch, error_payload, task_id)
                ch.basic_ack(delivery_tag=method.delivery_tag)
                return

            logger.info(f"Задача {task_id}: Начало транскрибации для {local_audio_file} (только русский язык)")
            transcription_result = transcribe_audio_russian(str(local_audio_file), task_id=task_id)

            if transcription_result:
                # ... (код для создания JSON без изменений)
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
                    "task_id": task_id, "status": "success", "service": "whisper",
                    "tool_version": whisper.__version__, "model_used": WHISPER_MODEL_NAME,
                    "input_bucket": current_bucket_name, "input_object": original_input_object,
                    "processed_object": input_object_name, 
                    "transcription_detailed_json_object_path": f"s3://{current_bucket_name}/{output_json_minio_object_name}",
                    "language_requested": "ru", "language_detected_by_model": transcription_result.get("language"),
                    "full_text": detailed_transcription_data["full_text"], "segments": detailed_transcription_data["segments"]
                }
                publish_result(ch, result_for_rabbitmq, task_id)
                ch.basic_ack(delivery_tag=method.delivery_tag)
                logger.info(f"Задача {task_id}: Успешно обработана, результаты опубликованы.")
            else:
                # ... (обработка ошибки транскрибации без изменений) ...
                # Добавим original_input_object в сообщение об ошибке для полноты картины
                error_payload = {"task_id": task_id, "status": "error", "service": "whisper", 
                                 "input_bucket": current_bucket_name, 
                                 "input_object": original_input_object,
                                 "processed_object": input_object_name,
                                 "error_message": "Transcription failed in whisper_worker."}
                publish_result(ch, error_payload, task_id)
                ch.basic_ack(delivery_tag=method.delivery_tag)
    
    except json.JSONDecodeError as e:
        logger.error(f"Задача {task_id}: Не удалось декодировать JSON сообщение: {e}. Тело: {body[:200]}")
        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False) 
    except pika.exceptions.AMQPConnectionError as e:
        logger.error(f"Задача {task_id}: Ошибка соединения RabbitMQ во время обработки: {e}")
        # Не делаем ack/nack, позволяем ошибке всплыть, чтобы главный цикл переподключился
        raise
    except Exception as e:
        logger.error(f"Задача {task_id}: Необработанное исключение в callback: {e}")
        logger.error(traceback.format_exc())
        error_payload = {"task_id": task_id, "status": "critical_error", "service": "whisper", "error_message": f"Unhandled exception in callback: {str(e)}"}
        try:
            if ch.is_open: publish_result(ch, error_payload, task_id)
        except Exception as pub_e:
            logger.error(f"Задача {task_id}: Не удалось опубликовать сообщение о критической ошибке: {pub_e}")
        finally:
            if ch.is_open: ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)

# ИСПРАВЛЕНО: main() теперь проще и надежнее
def main():
    logger.info(f"Запуск whisper_worker с моделью: {WHISPER_MODEL_NAME}")
    logger.info(f"Слушает очередь '{CONSUME_QUEUE}'")
    logger.info(f"Публикует результаты в '{PUBLISH_EXCHANGE}' с ключом '{PUBLISH_ROUTING_KEY}'")

    try:
        get_minio_client()
    except Exception as e:
        logger.critical(f"Критическая ошибка: Не удалось подключиться к MinIO при старте: {e}. Воркер не будет запущен.")
        return

    connection = None
    while True:
        try:
            logger.info(f"Попытка подключения к RabbitMQ: {RABBITMQ_HOST}:{RABBITMQ_PORT}...")
            credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASS)
            parameters = pika.ConnectionParameters(
                RABBITMQ_HOST, RABBITMQ_PORT, RABBITMQ_VHOST, credentials,
                heartbeat=600, blocked_connection_timeout=300
            )
            connection = pika.BlockingConnection(parameters)
            channel = connection.channel()
            logger.info("Успешное подключение к RabbitMQ.")

            # Воркер НЕ должен объявлять инфраструктуру, кроме той, что ему нужна для работы.
            # Этим занимается SoundService (объявление очередей) или сам воркер (объявление своей очереди).
            # В нашем случае SoundService объявляет все, воркер только проверяет.
            # Для надежности, объявим то, что используем.
            # ИЗМЕНЕНО: Тип обменника на topic
            channel.exchange_declare(exchange=CONSUME_EXCHANGE, exchange_type='topic', durable=True)
            channel.exchange_declare(exchange=PUBLISH_EXCHANGE, exchange_type='topic', durable=True)

            # Воркер объявляет только СВОЮ очередь задач.
            channel.queue_declare(queue=CONSUME_QUEUE, durable=True)
            channel.queue_bind(exchange=CONSUME_EXCHANGE, queue=CONSUME_QUEUE, routing_key=CONSUME_ROUTING_KEY)
            
            channel.basic_qos(prefetch_count=1)
            channel.basic_consume(queue=CONSUME_QUEUE, on_message_callback=callback)
            
            logger.info(f"[*] Ожидание сообщений в очереди '{CONSUME_QUEUE}'. Для выхода нажмите CTRL+C")
            channel.start_consuming()

        except pika.exceptions.AMQPConnectionError as e:
            logger.warning(f"Потеряно соединение с RabbitMQ: {e}")
        except KeyboardInterrupt:
            logger.info("Получен сигнал KeyboardInterrupt. Завершение работы...")
            if channel and channel.is_open:
                channel.stop_consuming()
            break
        except Exception as e:
            logger.error(f"Произошла непредвиденная ошибка в главном цикле: {e}")
            logger.error(traceback.format_exc())
        finally:
            if connection and connection.is_open:
                try: connection.close()
                except Exception as co_ex: logger.warning(f"Ошибка при закрытии соединения: {co_ex}")
            logger.info(f"Повторная попытка через {RECONNECT_DELAY_SECONDS} секунд...")
            time.sleep(RECONNECT_DELAY_SECONDS)

    logger.info("Воркер whisper_worker остановлен.")

if __name__ == "__main__":
    main()