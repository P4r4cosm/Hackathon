import os
import uuid
import subprocess
import pika
import json
import torch
import shutil
import time
import tempfile
from minio import Minio
from minio.error import S3Error
import logging

# --- Конфигурация логирования ---
logging.basicConfig(
    level=os.getenv('LOG_LEVEL', 'INFO').upper(),
    format='%(asctime)s - %(levelname)s - %(process)d - %(threadName)s - %(module)s - %(funcName)s - %(message)s'
)
logger = logging.getLogger(__name__)

# --- Конфигурация из переменных окружения ---
RABBITMQ_HOST = os.getenv('RABBITMQ_HOST', 'localhost')
RABBITMQ_PORT = int(os.getenv('RABBITMQ_PORT', 5672))
RABBITMQ_USER = os.getenv('RABBITMQ_USER', 'user')
RABBITMQ_PASS = os.getenv('RABBITMQ_PASS', 'password')
RABBITMQ_VHOST = os.getenv('RABBITMQ_VHOST', '/')

RABBITMQ_CONSUME_QUEUE_NAME = os.getenv('RABBITMQ_CONSUME_QUEUE_NAME', 'demucs_tasks_queue') # Очередь, которую слушаем
RABBITMQ_CONSUME_EXCHANGE_NAME = os.getenv('RABBITMQ_CONSUME_EXCHANGE_NAME', 'audio_processing_exchange') # Exchange, к которому привязана очередь
RABBITMQ_CONSUME_ROUTING_KEY = os.getenv('RABBITMQ_CONSUME_ROUTING_KEY', 'demucs.task') # Ключ для привязки

RABBITMQ_PUBLISH_EXCHANGE_NAME = os.getenv('RABBITMQ_PUBLISH_EXCHANGE_NAME', 'results_exchange') # Exchange для результатов
RABBITMQ_PUBLISH_ROUTING_KEY = os.getenv('RABBITMQ_PUBLISH_ROUTING_KEY', 'task.result') # Ключ для результатов

MINIO_ENDPOINT = os.getenv('MINIO_ENDPOINT', 'localhost:9000')
MINIO_ACCESS_KEY = os.getenv('MINIO_ACCESS_KEY')# Обязательно должны быть установлены
MINIO_SECRET_KEY = os.getenv('MINIO_SECRET_KEY')
MINIO_USE_SSL = os.getenv('MINIO_USE_SSL', 'False').lower() == 'true'
MINIO_DEFAULT_BUCKET = os.getenv('MINIO_BUCKET_NAME', 'audio-bucket') # Бакет по умолчанию для операций

DEMUCS_MODEL = os.getenv('DEMUCS_MODEL', "htdemucs")
DEMUCS_SHIFTS = int(os.getenv('DEMUCS_SHIFTS', 0)) # 0 для лучшего качества, >0 для скорости
DEMUCS_DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

RECONNECT_DELAY_SECONDS = 5
MAX_RETRIES_RABBITMQ = int(os.getenv('MAX_RETRIES_RABBITMQ', 5)) # Для переподключения к RabbitMQ

# --- Инициализация клиента MinIO ---
minio_client = None
try:
    if not MINIO_ACCESS_KEY or not MINIO_SECRET_KEY:
        logger.error("MINIO_ACCESS_KEY and MINIO_SECRET_KEY environment variables must be set.")
        exit(1)
    minio_client = Minio(
        MINIO_ENDPOINT,
        access_key=MINIO_ACCESS_KEY,
        secret_key=MINIO_SECRET_KEY,
        secure=MINIO_USE_SSL
    )
    logger.info(f"Successfully initialized MinIO client for endpoint {MINIO_ENDPOINT}")
except Exception as e:
    logger.error(f"Could not initialize MinIO client: {e}")
    exit(1)


def ensure_minio_bucket_exists(bucket_name):
    """Проверяет существование бакета и создает его, если необходимо."""
    try:
        buckets = minio_client.list_buckets()
        for b in buckets:
            logger.info(f"Found bucket: {b.name}")
        logger.info(f"Successfully listed buckets. Now checking for '{bucket_name}'...")


        found = minio_client.bucket_exists(bucket_name)
        if not found:
            minio_client.make_bucket(bucket_name)
            logger.info(f"MinIO bucket '{bucket_name}' created.")
        else:
            logger.info(f"MinIO bucket '{bucket_name}' already exists.")
    except S3Error as e:
        logger.error(f"MinIO error checking or creating bucket {bucket_name}: {e}")
        raise # Перебрасываем, чтобы обработать выше


def publish_processing_result(channel, task_id, status, result_data):
    """Отправляет результат обработки обратно в RabbitMQ."""
    message_payload = {
        "task_id": task_id,
        "status": status,
        **result_data
    }
    try:
        channel.basic_publish(
            exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME,
            routing_key=RABBITMQ_PUBLISH_ROUTING_KEY,
            body=json.dumps(message_payload),
            properties=pika.BasicProperties(
                delivery_mode=pika.spec.PERSISTENT_DELIVERY_MODE,
                content_type='application/json',
                correlation_id=task_id # Полезно для связи
            )
        )
        logger.info(f"Published result for task_id {task_id}: status='{status}'")
    except Exception as e:
        logger.error(f"Failed to publish result for task_id {task_id}: {e}")
        # Здесь можно добавить логику повторной отправки или обработки ошибки


def run_demucs_process(task_id, input_audio_path, base_output_dir):
    """Запускает процесс Demucs."""
    demucs_output_subpath = os.path.join(DEMUCS_MODEL, os.path.splitext(os.path.basename(input_audio_path))[0])
    expected_vocals_file = os.path.join(base_output_dir, demucs_output_subpath, "vocals.wav")

    cmd = [
        "python3", "-m", "demucs",
        "-d", DEMUCS_DEVICE,
        "-n", DEMUCS_MODEL,
        "--two-stems", "vocals",
        "--shifts", str(DEMUCS_SHIFTS),
        "--out", base_output_dir,
        input_audio_path
    ]
    logger.info(f"Task {task_id}: Executing Demucs: {' '.join(cmd)}")
    try:
        process = subprocess.run(cmd, capture_output=True, text=True, check=False)
        logger.debug(f"Task {task_id}: Demucs stdout:\n{process.stdout}")
        if process.stderr:
            logger.warning(f"Task {task_id}: Demucs stderr:\n{process.stderr}")

        if process.returncode != 0:
            return None, {
                "error_message": "Demucs processing failed",
                "details": {"stdout": process.stdout, "stderr": process.stderr, "returncode": process.returncode}
            }

        if not os.path.exists(expected_vocals_file):
            # Дополнительная проверка, если стандартный путь не сработал
            logger.warning(f"Task {task_id}: Expected vocals file not found at {expected_vocals_file}. Searching...")
            found_vocals = []
            for root, _, files in os.walk(base_output_dir):
                for f_name in files:
                    if "vocals.wav" in f_name.lower(): # Ищем без учета регистра
                        found_vocals.append(os.path.join(root, f_name))
            if found_vocals:
                logger.info(f"Task {task_id}: Found vocals at: {found_vocals[0]}. Using this file.")
                return found_vocals[0], None # Берем первый найденный
            return None, {
                "error_message": f"Demucs output vocals.wav not found. Expected: {expected_vocals_file}",
                "details": { "search_path": base_output_dir, "demucs_stdout": process.stdout }
            }

        logger.info(f"Task {task_id}: Demucs processing successful. Vocals at {expected_vocals_file}")
        return expected_vocals_file, None

    except Exception as e:
        logger.exception(f"Task {task_id}: Exception during Demucs execution: {e}")
        return None, {"error_message": f"Demucs execution exception: {str(e)}"}


def process_single_task(task_id, input_bucket, input_object_name, output_file_basename):
    """Полный цикл обработки одной задачи: скачать, Demucs, загрузить."""
    with tempfile.TemporaryDirectory(prefix="demucs_worker_") as temp_dir:
        local_input_dir = os.path.join(temp_dir, "input")
        local_output_base_dir = os.path.join(temp_dir, "output")
        os.makedirs(local_input_dir, exist_ok=True)
        os.makedirs(local_output_base_dir, exist_ok=True)

        _, file_extension = os.path.splitext(input_object_name)
        local_input_filename = f"{task_id}{file_extension}" # Используем task_id для уникальности
        local_input_path = os.path.join(local_input_dir, local_input_filename)

        # 1. Скачать файл из MinIO
        logger.info(f"Task {task_id}: Downloading s3://{input_bucket}/{input_object_name} to {local_input_path}")
        try:
            minio_client.fget_object(input_bucket, input_object_name, local_input_path)
        except S3Error as e:
            logger.error(f"Task {task_id}: MinIO download failed: {e}")
            return {"error_message": f"MinIO download error: {str(e)}", "details": {"bucket": input_bucket, "object": input_object_name}}

        # 2. Запустить Demucs
        vocals_file_path, demucs_error = run_demucs_process(task_id, local_input_path, local_output_base_dir)
        if demucs_error:
            return demucs_error # Возвращаем словарь с ошибкой

        # 3. Загрузить результат в MinIO
        minio_output_object_name = f"results/demucs/{task_id}_{output_file_basename}_vocals.wav"
        logger.info(f"Task {task_id}: Uploading {vocals_file_path} to s3://{input_bucket}/{minio_output_object_name}")
        try:
            minio_client.fput_object(
                input_bucket,
                minio_output_object_name,
                vocals_file_path,
                content_type='audio/wav'
            )
            logger.info(f"Task {task_id}: Successfully uploaded result to MinIO.")
            return { # Данные для успешного сообщения
                "output_bucket_name": input_bucket,
                "output_object_name": minio_output_object_name,
                "message": "Demucs processing completed successfully."
            }
        except S3Error as e:
            logger.error(f"Task {task_id}: MinIO upload failed: {e}")
            return {"error_message": f"MinIO upload error: {str(e)}", "details": {"bucket": input_bucket, "object": minio_output_object_name}}
        except Exception as e: # Широкий перехват на случай других ошибок при загрузке
            logger.exception(f"Task {task_id}: Exception during MinIO upload: {e}")
            return {"error_message": f"MinIO upload exception: {str(e)}"}


def on_message_callback(channel, method_frame, properties, body):
    """Обработчик входящего сообщения от RabbitMQ."""
    task_id_from_msg = "unknown_task" # Для логирования ошибок до парсинга task_id
    try:
        message_str = body.decode('utf-8')
        logger.info(f"Received raw message (delivery_tag={method_frame.delivery_tag}): {message_str}")
        task_data = json.loads(message_str)

        # Используем поля из C# модели (с JsonPropertyName) или ожидаемые snake_case
        task_id_from_msg = task_data.get("task_id") or task_data.get("TaskId") or str(uuid.uuid4())
        input_bucket = task_data.get("input_bucket_name") or MINIO_DEFAULT_BUCKET
        input_object = task_data.get("input_object_name") or task_data.get("MinioFilePath")
        output_basename = task_data.get("output_file_basename") or \
                          (os.path.splitext(os.path.basename(input_object))[0] if input_object else task_id_from_msg)


        if not input_object: # input_bucket может быть по умолчанию
            logger.error(f"Task {task_id_from_msg}: Missing 'input_object_name' or 'MinioFilePath' in message.")
            publish_processing_result(channel, task_id_from_msg, "error",
                                      {"error_message": "Invalid task: missing input object path."})
            channel.basic_ack(delivery_tag=method_frame.delivery_tag)
            return

        logger.info(f"Task {task_id_from_msg}: Processing s3://{input_bucket}/{input_object}")
        logger.info(f"Device for Demucs: {DEMUCS_DEVICE}. PyTorch version: {torch.__version__}. CUDA available: {torch.cuda.is_available()}")

        result_payload = process_single_task(task_id_from_msg, input_bucket, input_object, output_basename)

        if "error_message" in result_payload:
            publish_processing_result(channel, task_id_from_msg, "error", result_payload)
        else:
            publish_processing_result(channel, task_id_from_msg, "success", result_payload)

        channel.basic_ack(delivery_tag=method_frame.delivery_tag)
        logger.info(f"Task {task_id_from_msg} (delivery_tag={method_frame.delivery_tag}) acknowledged.")

    except json.JSONDecodeError as e:
        logger.error(f"Failed to decode JSON message body: {body[:200]}... Error: {e}")
        channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False) # Не переотправлять, т.к. битое
    except Exception as e:
        logger.exception(f"Unhandled exception while processing task {task_id_from_msg} (delivery_tag={method_frame.delivery_tag}): {e}")
        publish_processing_result(channel, task_id_from_msg, "error", {"error_message": f"Unhandled worker exception: {str(e)}"})
        # Решить, переотправлять ли. Для большинства необработанных лучше не надо.
        channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)


def main():
    logger.info("Demucs Worker starting...")
    logger.info(f"Demucs model: {DEMUCS_MODEL}, Shifts: {DEMUCS_SHIFTS}, Device: {DEMUCS_DEVICE}")

    try:
        ensure_minio_bucket_exists(MINIO_DEFAULT_BUCKET)
        # Если есть другие бакеты, которые должны существовать, можно проверить и их
    except Exception:
        logger.error(f"Critical error ensuring MinIO bucket '{MINIO_DEFAULT_BUCKET}' exists. Worker cannot start.")
        return

    connection = None
    retries = 0
    while retries < MAX_RETRIES_RABBITMQ or MAX_RETRIES_RABBITMQ == 0: # 0 для бесконечных попыток
        try:
            logger.info(f"Attempting to connect to RabbitMQ: {RABBITMQ_HOST}:{RABBITMQ_PORT} (Attempt {retries + 1})")
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
            logger.info("Successfully connected to RabbitMQ.")
            retries = 0 # Сбрасываем счетчик при успешном подключении

            # Идемпотентное объявление инфраструктуры (для консьюмера)
            channel.exchange_declare(exchange=RABBITMQ_CONSUME_EXCHANGE_NAME, exchange_type='direct', durable=True)
            channel.queue_declare(queue=RABBITMQ_CONSUME_QUEUE_NAME, durable=True)
            channel.queue_bind(
                exchange=RABBITMQ_CONSUME_EXCHANGE_NAME,
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                routing_key=RABBITMQ_CONSUME_ROUTING_KEY
            )

            # Идемпотентное объявление инфраструктуры (для продюсера результатов)
            channel.exchange_declare(exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME, exchange_type='direct', durable=True)
            # Очередь для результатов объявляется и слушается C# сервисом.

            channel.basic_qos(prefetch_count=1) # По одной задаче за раз
            channel.basic_consume(
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                on_message_callback=on_message_callback
                # auto_ack=False по умолчанию, это правильно
            )

            logger.info(f"[*] Waiting for tasks on queue '{RABBITMQ_CONSUME_QUEUE_NAME}'. To exit press CTRL+C")
            channel.start_consuming()

        except pika.exceptions.AMQPConnectionError as e:
            logger.error(f"RabbitMQ connection/channel error: {e}")
            if connection and not connection.is_closed:
                try: connection.close()
                except: pass # Игнорируем ошибки при закрытии
            retries += 1
            if MAX_RETRIES_RABBITMQ > 0 and retries >= MAX_RETRIES_RABBITMQ:
                logger.error(f"Max RabbitMQ connection retries ({MAX_RETRIES_RABBITMQ}) reached. Exiting.")
                break
            logger.info(f"Retrying in {RECONNECT_DELAY_SECONDS} seconds...")
            time.sleep(RECONNECT_DELAY_SECONDS)
        except KeyboardInterrupt:
            logger.info("Demucs Worker shutting down gracefully by user interrupt...")
            if connection and channel and channel.is_open:
                channel.stop_consuming() # Остановить прием новых сообщений
                # Дать время на завершение текущих задач (если нужно, но QOS=1 упрощает)
            if connection and connection.is_open:
                connection.close()
            logger.info("RabbitMQ connection closed.")
            break
        except Exception as e: # Любые другие неожиданные ошибки в главном цикле
            logger.exception(f"An unexpected critical error occurred in main loop: {e}")
            if connection and not connection.is_closed:
                try: connection.close()
                except: pass
            retries += 1 # Считаем это как ошибку подключения, чтобы не зациклиться
            if MAX_RETRIES_RABBITMQ > 0 and retries >= MAX_RETRIES_RABBITMQ:
                logger.error(f"Max retries reached after critical error. Exiting.")
                break
            logger.info(f"Retrying in {RECONNECT_DELAY_SECONDS * 2} seconds after critical error...") # Увеличим задержку
            time.sleep(RECONNECT_DELAY_SECONDS * 2)

    logger.info("Demucs Worker stopped.")


if __name__ == '__main__':
    main()