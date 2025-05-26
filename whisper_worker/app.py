import os
import json
import logging
import time
import traceback
from pathlib import Path
import tempfile

import pika
from minio import Minio
from minio.error import S3Error
from dotenv import load_dotenv
import whisper

# Загрузка переменных окружения из .env файла
load_dotenv()

# Настройка логирования
LOG_LEVEL = os.getenv("LOG_LEVEL", "INFO").upper()
logging.basicConfig(
    level=LOG_LEVEL, format="%(asctime)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# --- RabbitMQ Configuration ---
RABBITMQ_HOST = os.getenv("RABBITMQ_HOST", "rabbitmq")
RABBITMQ_PORT = int(os.getenv("RABBITMQ_PORT", 5672))
RABBITMQ_USER = os.getenv("RABBITMQ_USER", "guest")
RABBITMQ_PASS = os.getenv("RABBITMQ_PASS", "guest")
RABBITMQ_VHOST = os.getenv("RABBITMQ_VHOST", "/")

RABBITMQ_CONSUME_QUEUE_NAME = os.getenv("RABBITMQ_WHISPER_QUEUE_NAME", "whisper_tasks_queue")
RABBITMQ_CONSUME_EXCHANGE_NAME = os.getenv("RABBITMQ_AUDIO_PROCESSING_EXCHANGE", "audio_processing_exchange")
RABBITMQ_CONSUME_ROUTING_KEY = os.getenv("RABBITMQ_WHISPER_TASKS_ROUTING_KEY", "whisper.task")

RABBITMQ_PUBLISH_EXCHANGE_NAME = os.getenv("RABBITMQ_RESULTS_EXCHANGE_NAME", "results_exchange")
RABBITMQ_PUBLISH_ROUTING_KEY_PREFIX = os.getenv("RABBITMQ_TASK_RESULTS_ROUTING_KEY_PREFIX", "task.result") # e.g., task.result.whisper

# --- MinIO Configuration ---
MINIO_ENDPOINT = os.getenv("MINIO_ENDPOINT", "minio:9000")
MINIO_ACCESS_KEY = os.getenv("MINIO_ACCESS_KEY", "minioadmin")
MINIO_SECRET_KEY = os.getenv("MINIO_SECRET_KEY", "minioadmin")
MINIO_BUCKET_NAME = os.getenv("MINIO_BUCKET_NAME", "audio-processing")
MINIO_USE_SSL = os.getenv("MINIO_USE_SSL", "False").lower() == "true"

# --- Whisper Configuration ---
WHISPER_MODEL_NAME = os.getenv("WHISPER_MODEL_NAME", "base") # "tiny", "base", "small", "medium", "large"
WHISPER_LANGUAGE = os.getenv("WHISPER_LANGUAGE") # e.g., "en", "ru". If None, auto-detects.
WHISPER_CACHE_DIR = os.getenv("WHISPER_CACHE_DIR", "/app/.cache/whisper")

# --- Connection Retries ---
MAX_RETRIES_RABBITMQ = int(os.getenv("MAX_RETRIES_RABBITMQ", 5))
RECONNECT_DELAY_SECONDS = int(os.getenv("RECONNECT_DELAY_SECONDS", 5))


def get_minio_client():
    """Инициализирует и возвращает клиент MinIO."""
    try:
        client = Minio(
            MINIO_ENDPOINT,
            access_key=MINIO_ACCESS_KEY,
            secret_key=MINIO_SECRET_KEY,
            secure=MINIO_USE_SSL,
        )
        logger.info(f"MinIO client initialized for endpoint: {MINIO_ENDPOINT}")
        return client
    except Exception as e:
        logger.error(f"Failed to initialize MinIO client: {e}")
        raise


def download_file_from_minio(minio_client, bucket_name, object_name, download_path):
    """Загружает файл из MinIO."""
    try:
        minio_client.fget_object(bucket_name, object_name, download_path)
        logger.info(f"File {object_name} downloaded from bucket {bucket_name} to {download_path}")
        return True
    except S3Error as e:
        logger.error(f"MinIO S3Error on download: {e}. Bucket: {bucket_name}, Object: {object_name}")
    except Exception as e:
        logger.error(f"Failed to download file {object_name} from MinIO: {e}")
    return False


def upload_file_to_minio(minio_client, bucket_name, file_path, object_name):
    """Загружает файл в MinIO."""
    try:
        # Убедимся, что бакет существует
        found = minio_client.bucket_exists(bucket_name)
        if not found:
            minio_client.make_bucket(bucket_name)
            logger.info(f"Bucket {bucket_name} created.")
        else:
            logger.info(f"Bucket {bucket_name} already exists.")

        minio_client.fput_object(bucket_name, object_name, file_path)
        logger.info(f"File {file_path} uploaded to MinIO as {object_name} in bucket {bucket_name}")
        return True
    except S3Error as e:
        logger.error(f"MinIO S3Error on upload: {e}. Bucket: {bucket_name}, Object: {object_name}, File: {file_path}")
    except Exception as e:
        logger.error(f"Failed to upload file {file_path} to MinIO: {e}")
    return False


def transcribe_audio(audio_file_path, model_name=WHISPER_MODEL_NAME, language=WHISPER_LANGUAGE, cache_dir=WHISPER_CACHE_DIR):
    """
    Распознает речь в аудиофайле с использованием Whisper.
    Возвращает результат в виде словаря (текст, сегменты и т.д.).
    """
    try:
        logger.info(f"Loading Whisper model: {model_name} with cache_dir: {cache_dir}")
        # Убедимся, что директория для кэша существует и доступна для записи
        Path(cache_dir).mkdir(parents=True, exist_ok=True)
        os.chmod(cache_dir, 0o777) # Даем права, если они были сброшены

        model = whisper.load_model(model_name, download_root=cache_dir)
        logger.info(f"Whisper model {model_name} loaded. Starting transcription for {audio_file_path}...")

        transcribe_options = {"fp16": False} # Установите True, если у вас GPU с поддержкой fp16
        if language:
            transcribe_options["language"] = language

        result = model.transcribe(audio_file_path, **transcribe_options)
        logger.info(f"Transcription successful for {audio_file_path}. Detected language: {result.get('language')}")
        return result
    except Exception as e:
        logger.error(f"Error during audio transcription: {e}")
        logger.error(traceback.format_exc())
        return None

def publish_result(channel, result_message, task_id, service_name="whisper"):
    """Публикует результат обработки в RabbitMQ."""
    try:
        routing_key = f"{RABBITMQ_PUBLISH_ROUTING_KEY_PREFIX}.{service_name}"
        # Добавляем task_id в сообщение, если его еще нет
        if isinstance(result_message, dict) and 'task_id' not in result_message:
            result_message['task_id'] = task_id
        elif isinstance(result_message, str): # если результат просто текст
             result_message = {'task_id': task_id, 'text': result_message}


        channel.basic_publish(
            exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME,
            routing_key=routing_key,
            body=json.dumps(result_message),
            properties=pika.BasicProperties(
                delivery_mode=pika.spec.PERSISTENT_DELIVERY_MODE,
                correlation_id=task_id # Используем task_id как correlation_id
            )
        )
        logger.info(f"Result for task_id {task_id} published to {RABBITMQ_PUBLISH_EXCHANGE_NAME} with routing key {routing_key}")
    except Exception as e:
        logger.error(f"Failed to publish result for task_id {task_id}: {e}")


def callback(ch, method, properties, body):
    """Обработчик сообщений из RabbitMQ."""
    task_id = properties.correlation_id # Получаем task_id из correlation_id
    try:
        message = json.loads(body.decode())
        logger.info(f"Received message for task_id: {task_id}, body: {message}")

        input_object_name = message.get("input_minio_object_name")
        output_prefix = message.get("output_minio_prefix", "whisper_output/") # Префикс для выходных файлов

        if not input_object_name:
            logger.error(f"Task_id {task_id}: Missing 'input_minio_object_name' in message.")
            ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False) # Не переотправлять
            return

        minio_client = get_minio_client()

        with tempfile.TemporaryDirectory() as tmpdir:
            local_audio_file = Path(tmpdir) / Path(input_object_name).name
            
            logger.info(f"Task_id {task_id}: Downloading {input_object_name} to {local_audio_file}")
            if not download_file_from_minio(minio_client, MINIO_BUCKET_NAME, input_object_name, str(local_audio_file)):
                logger.error(f"Task_id {task_id}: Failed to download {input_object_name}. Skipping.")
                ch.basic_nack(delivery_tag=method.delivery_tag, requeue=True) # Можно попробовать переотправить позже
                return

            logger.info(f"Task_id {task_id}: Starting transcription for {local_audio_file}")
            transcription_result = transcribe_audio(str(local_audio_file))

            if transcription_result:
                # Сохраняем полный результат транскрибации в JSON
                output_json_filename = f"{Path(input_object_name).stem}_transcription.json"
                output_json_path = Path(tmpdir) / output_json_filename
                with open(output_json_path, 'w', encoding='utf-8') as f:
                    json.dump(transcription_result, f, ensure_ascii=False, indent=4)
                
                output_json_minio_object_name = f"{output_prefix.rstrip('/')}/{output_json_filename}"
                upload_file_to_minio(minio_client, MINIO_BUCKET_NAME, str(output_json_path), output_json_minio_object_name)

                # Сохраняем только текст в .txt файл
                output_txt_filename = f"{Path(input_object_name).stem}_transcription.txt"
                output_txt_path = Path(tmpdir) / output_txt_filename
                with open(output_txt_path, 'w', encoding='utf-8') as f:
                    f.write(transcription_result.get("text", ""))
                
                output_txt_minio_object_name = f"{output_prefix.rstrip('/')}/{output_txt_filename}"
                upload_file_to_minio(minio_client, MINIO_BUCKET_NAME, str(output_txt_path), output_txt_minio_object_name)

                result_for_rabbitmq = {
                    "task_id": task_id,
                    "status": "success",
                    "service": "whisper",
                    "original_input_object": input_object_name,
                    "transcription_text_object": output_txt_minio_object_name,
                    "transcription_json_object": output_json_minio_object_name,
                    "detected_language": transcription_result.get("language")
                }
                publish_result(ch, result_for_rabbitmq, task_id, service_name="whisper")
                ch.basic_ack(delivery_tag=method.delivery_tag)
                logger.info(f"Task_id {task_id}: Successfully processed and results published.")
            else:
                logger.error(f"Task_id {task_id}: Transcription failed for {input_object_name}.")
                # Отправка сообщения об ошибке
                error_message = {
                    "task_id": task_id,
                    "status": "error",
                    "service": "whisper",
                    "original_input_object": input_object_name,
                    "error_message": "Transcription failed in whisper_worker."
                }
                publish_result(ch, error_message, task_id, service_name="whisper.error") # Отдельный роутинг для ошибок
                ch.basic_ack(delivery_tag=method.delivery_tag) # Подтверждаем, т.к. обработали (сообщили об ошибке)
        
    except json.JSONDecodeError as e:
        logger.error(f"Task_id {task_id}: Failed to decode JSON message: {e}. Body: {body}")
        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False) # Сообщение битое
    except Exception as e:
        logger.error(f"Task_id {task_id}: Unhandled exception in callback: {e}")
        logger.error(traceback.format_exc())
        # Вместо nack с requeue=True, лучше отправить сообщение об ошибке, если это возможно
        # или nack без requeue, чтобы избежать бесконечного цикла обработки сбойного сообщения.
        try:
            error_message = {
                "task_id": task_id,
                "status": "critical_error",
                "service": "whisper",
                "error_message": f"Unhandled exception in callback: {str(e)}"
            }
            # Попытка отправить сообщение об ошибке (канал может быть уже закрыт)
            if ch.is_open:
                 publish_result(ch, error_message, task_id, service_name="whisper.error")
        except Exception as pub_e:
            logger.error(f"Task_id {task_id}: Failed to publish critical error message: {pub_e}")

        if ch.is_open:
            ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False) # Не переотправлять


def main():
    """Основная функция запуска воркера."""
    minio_client = get_minio_client() # Проверим подключение к MinIO при старте
    if not minio_client:
        logger.error("Could not connect to MinIO. Exiting.")
        return

    connection = None
    for attempt in range(MAX_RETRIES_RABBITMQ):
        try:
            logger.info(f"Connecting to RabbitMQ (attempt {attempt + 1}/{MAX_RETRIES_RABBITMQ})...")
            credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASS)
            parameters = pika.ConnectionParameters(
                RABBITMQ_HOST,
                RABBITMQ_PORT,
                RABBITMQ_VHOST,
                credentials,
                heartbeat=600,  # Для поддержания соединения
                blocked_connection_timeout=300 # Таймаут блокировки
            )
            connection = pika.BlockingConnection(parameters)
            channel = connection.channel()
            logger.info("Successfully connected to RabbitMQ.")

            # Объявление обменника для задач (если его нет)
            channel.exchange_declare(exchange=RABBITMQ_CONSUME_EXCHANGE_NAME, exchange_type='direct', durable=True)
            # Объявление обменника для результатов (если его нет)
            channel.exchange_declare(exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME, exchange_type='direct', durable=True)

            # Объявление очереди для задач
            channel.queue_declare(queue=RABBITMQ_CONSUME_QUEUE_NAME, durable=True)
            channel.queue_bind(
                exchange=RABBITMQ_CONSUME_EXCHANGE_NAME,
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                routing_key=RABBITMQ_CONSUME_ROUTING_KEY
            )
            
            # Ограничение количества одновременно обрабатываемых сообщений (prefetch_count=1)
            # Это важно для задач, интенсивно использующих ресурсы (например, GPU)
            channel.basic_qos(prefetch_count=1)
            channel.basic_consume(queue=RABBITMQ_CONSUME_QUEUE_NAME, on_message_callback=callback)

            logger.info(f"[*] Waiting for messages in queue '{RABBITMQ_CONSUME_QUEUE_NAME}'. To exit press CTRL+C")
            channel.start_consuming()
            break # Выход из цикла, если подключение и старт успешны
        except pika.exceptions.AMQPConnectionError as e:
            logger.error(f"RabbitMQ connection failed (attempt {attempt + 1}): {e}")
            if attempt < MAX_RETRIES_RABBITMQ - 1:
                time.sleep(RECONNECT_DELAY_SECONDS)
            else:
                logger.error("Max retries reached. Could not connect to RabbitMQ.")
                return # Выход, если все попытки неудачны
        except Exception as e:
            logger.error(f"An unexpected error occurred: {e}")
            logger.error(traceback.format_exc())
            if connection and connection.is_open:
                connection.close()
            return # Выход при других ошибках

    logger.info("Shutting down whisper_worker.")
    if connection and connection.is_open:
        connection.close()

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        logger.info("KeyboardInterrupt received. Shutting down...")
    except Exception as e:
        logger.critical(f"Critical error in main execution: {e}")
        logger.critical(traceback.format_exc()) 