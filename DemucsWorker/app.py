import os
import sys
import uuid
import subprocess
import pika
import json
import torch
import time
import tempfile
from minio import Minio
from minio.error import S3Error
import logging

# --- Конфигурация логирования ---
logging.basicConfig(
    level=os.getenv('LOG_LEVEL', 'INFO').upper(),
    format='%(asctime)s - %(levelname)s - %(process)d - %(module)s - %(funcName)s - %(message)s'
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
MINIO_ENDPOINT = os.getenv('MINIO_ENDPOINT', 'localhost:9000')
MINIO_ACCESS_KEY = os.getenv('MINIO_ACCESS_KEY')
MINIO_SECRET_KEY = os.getenv('MINIO_SECRET_KEY')
MINIO_USE_SSL = os.getenv('MINIO_USE_SSL', 'False').lower() == 'true'
MINIO_DEFAULT_BUCKET = os.getenv('MINIO_BUCKET_NAME', 'audio-bucket')

DEMUCS_MODEL = os.getenv('DEMUCS_MODEL', "htdemucs")
DEMUCS_SHIFTS = int(os.getenv('DEMUCS_SHIFTS', 0))
DEMUCS_DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

RECONNECT_DELAY_SECONDS = int(os.getenv("RECONNECT_DELAY_SECONDS", 5))

MINIO_CLIENT = None

# --- Функции ---

def get_minio_client():
    # ... (код такой же, как в whisper_worker)
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

# ИСПРАВЛЕНО: функция публикации стала универсальной
def publish_processing_result(channel, task_id, status, result_data):
    message_payload = {
        "task_id": task_id,
        "service": "demucs",
        "status": status,
        **result_data
    }
    try:
        channel.basic_publish(
            exchange=PUBLISH_EXCHANGE,
            routing_key=PUBLISH_ROUTING_KEY,
            body=json.dumps(message_payload),
            properties=pika.BasicProperties(
                delivery_mode=pika.spec.PERSISTENT_DELIVERY_MODE,
                content_type='application/json',
                correlation_id=task_id
            )
        )
        logger.info(f"Результат для task_id {task_id} опубликован в '{PUBLISH_EXCHANGE}' с ключом '{PUBLISH_ROUTING_KEY}'")
    except Exception as e:
        logger.error(f"Не удалось опубликовать результат для task_id {task_id}: {e}")

# Функции run_demucs_process и process_single_task остаются без изменений
def run_demucs_process(task_id, input_audio_path, base_output_dir):
    # ... (код без изменений)
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
    logger.info(f"Задача {task_id}: Выполнение Demucs: {' '.join(cmd)}")
    try:
        process = subprocess.run(cmd, capture_output=True, text=True, check=False)
        logger.debug(f"Задача {task_id}: Demucs stdout:\n{process.stdout}")
        if process.stderr:
            logger.warning(f"Задача {task_id}: Demucs stderr:\n{process.stderr}")

        if process.returncode != 0:
            return None, {
                "error_message": "Ошибка обработки Demucs",
                "details": {"stdout": process.stdout, "stderr": process.stderr, "returncode": process.returncode}
            }

        if not os.path.exists(expected_vocals_file):
            logger.warning(f"Задача {task_id}: Ожидаемый файл с вокалом не найден по пути {expected_vocals_file}. Поиск...")
            found_vocals = []
            for root, _, files in os.walk(base_output_dir):
                for f_name in files:
                    if "vocals.wav" in f_name.lower():
                        found_vocals.append(os.path.join(root, f_name))
            if found_vocals:
                logger.info(f"Задача {task_id}: Найден файл с вокалом: {found_vocals[0]}. Используется этот файл.")
                return found_vocals[0], None
            return None, {
                "error_message": f"Выходной файл Demucs vocals.wav не найден. Ожидаемый путь: {expected_vocals_file}",
                "details": { "search_path": base_output_dir, "demucs_stdout": process.stdout }
            }

        logger.info(f"Задача {task_id}: Обработка Demucs успешна. Вокал по пути {expected_vocals_file}")
        return expected_vocals_file, None

    except Exception as e:
        logger.exception(f"Задача {task_id}: Исключение во время выполнения Demucs: {e}")
        return None, {"error_message": f"Исключение при выполнении Demucs: {str(e)}"}

def process_single_task(task_id, input_bucket, input_object_name, output_file_basename):
    # ... (код без изменений)
    with tempfile.TemporaryDirectory(prefix="demucs_worker_") as temp_dir:
        local_input_dir = os.path.join(temp_dir, "input")
        local_output_base_dir = os.path.join(temp_dir, "output")
        os.makedirs(local_input_dir, exist_ok=True)
        os.makedirs(local_output_base_dir, exist_ok=True)

        _, file_extension = os.path.splitext(input_object_name)
        local_input_filename = f"{task_id}{file_extension}"
        local_input_path = os.path.join(local_input_dir, local_input_filename)

        # 1. Скачать файл из MinIO
        logger.info(f"Задача {task_id}: Загрузка s3://{input_bucket}/{input_object_name} в {local_input_path}")
        minio_client_instance = get_minio_client()
        try:
            minio_client_instance.fget_object(input_bucket, input_object_name, local_input_path)
        except S3Error as e:
            logger.error(f"Задача {task_id}: Ошибка загрузки из MinIO: {e}")
            return {"error_message": f"Ошибка загрузки из MinIO: {str(e)}", "details": {"bucket": input_bucket, "object": input_object_name}}

        # 2. Запустить Demucs
        vocals_file_path, demucs_error = run_demucs_process(task_id, local_input_path, local_output_base_dir)
        if demucs_error:
            return demucs_error # Возвращаем словарь с ошибкой

        # 3. Загрузить результат в MinIO
        minio_output_object_name = f"results/demucs/{task_id}_{output_file_basename}_vocals.wav"
        logger.info(f"Задача {task_id}: Загрузка {vocals_file_path} в s3://{input_bucket}/{minio_output_object_name}")
        try:
            minio_client_instance.fput_object(
                input_bucket,
                minio_output_object_name,
                vocals_file_path,
                content_type='audio/wav'
            )
            logger.info(f"Задача {task_id}: Результат успешно загружен в MinIO.")
            return {
                "output_bucket_name": input_bucket,
                "output_object_name": minio_output_object_name,
                "message": "Обработка Demucs успешно завершена."
            }
        except S3Error as e:
            logger.error(f"Задача {task_id}: Ошибка выгрузки в MinIO: {e}")
            return {"error_message": f"Ошибка выгрузки в MinIO: {str(e)}", "details": {"bucket": input_bucket, "object": minio_output_object_name}}
        except Exception as e:
            logger.exception(f"Задача {task_id}: Исключение во время выгрузки в MinIO: {e}")
            return {"error_message": f"Исключение при выгрузке в MinIO: {str(e)}"}

# Код on_message_callback остается почти без изменений
def on_message_callback(channel, method_frame, properties, body):
    # ... (код без изменений)
    task_id_from_msg = "unknown_task"
    try:
        message_str = body.decode('utf-8')
        logger.info(f"Получено сырое сообщение (delivery_tag={method_frame.delivery_tag}): {message_str}")
        task_data = json.loads(message_str)

        task_id_from_msg = task_data.get("task_id") or task_data.get("TaskId") or str(uuid.uuid4())
        input_bucket = task_data.get("input_bucket_name") or MINIO_DEFAULT_BUCKET
        input_object = task_data.get("input_object_name") or task_data.get("MinioFilePath")
        output_basename = task_data.get("output_file_basename") or \
                          (os.path.splitext(os.path.basename(input_object))[0] if input_object else task_id_from_msg)


        if not input_object:
            logger.error(f"Задача {task_id_from_msg}: Отсутствует 'input_object_name' или 'MinioFilePath' в сообщении.")
            publish_processing_result(channel, task_id_from_msg, "error",
                                      {"error_message": "Неверная задача: отсутствует путь к входному объекту."})
            channel.basic_ack(delivery_tag=method_frame.delivery_tag)
            return

        logger.info(f"Задача {task_id_from_msg}: Обработка s3://{input_bucket}/{input_object}")
        logger.info(f"Устройство для Demucs: {DEMUCS_DEVICE}. Версия PyTorch: {torch.__version__}. CUDA доступно: {torch.cuda.is_available()}")

        result_payload = process_single_task(task_id_from_msg, input_bucket, input_object, output_basename)

        if "error_message" in result_payload:
            publish_processing_result(channel, task_id_from_msg, "error", result_payload)
        else:
            publish_processing_result(channel, task_id_from_msg, "success", result_payload)

        channel.basic_ack(delivery_tag=method_frame.delivery_tag)
        logger.info(f"Задача {task_id_from_msg} (delivery_tag={method_frame.delivery_tag}) подтверждена.")

    except json.JSONDecodeError as e:
        logger.error(f"Не удалось декодировать JSON сообщение: {body[:200]}... Ошибка: {e}")
        channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
    except pika.exceptions.AMQPConnectionError as e:
        logger.error(f"Задача {task_id_from_msg}: Ошибка соединения RabbitMQ во время обработки: {e}")
        raise
    except Exception as e:
        logger.exception(f"Необработанное исключение при обработке задачи {task_id_from_msg} (delivery_tag={method_frame.delivery_tag}): {e}")
        try:
            error_msg_payload = {"task_id": task_id_from_msg, "status": "error", "error_message": f"Необработанное исключение воркера: {str(e)}"}
            publish_processing_result(channel, task_id_from_msg, "error", error_msg_payload)
        except Exception as pub_e:
            logger.error(f"Не удалось опубликовать критическое сообщение об ошибке для задачи {task_id_from_msg}: {pub_e}")
        channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)

# ИСПРАВЛЕНО: main() теперь проще и надежнее, как в whisper_worker
def main():
    logger.info(f"Demucs Worker запускается... Устройство: {DEMUCS_DEVICE}")
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

            # ИЗМЕНЕНО: Тип обменника на topic
            channel.exchange_declare(exchange=CONSUME_EXCHANGE, exchange_type='topic', durable=True)
            channel.exchange_declare(exchange=PUBLISH_EXCHANGE, exchange_type='topic', durable=True)

            channel.queue_declare(queue=CONSUME_QUEUE, durable=True)
            channel.queue_bind(exchange=CONSUME_EXCHANGE, queue=CONSUME_QUEUE, routing_key=CONSUME_ROUTING_KEY)

            channel.basic_qos(prefetch_count=1)
            channel.basic_consume(queue=CONSUME_QUEUE, on_message_callback=on_message_callback)

            logger.info(f"[*] Ожидание задач в очереди '{CONSUME_QUEUE}'. Для выхода нажмите CTRL+C")
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
        finally:
            if connection and connection.is_open:
                connection.close()
            logger.info(f"Повторная попытка через {RECONNECT_DELAY_SECONDS} секунд...")
            time.sleep(RECONNECT_DELAY_SECONDS)

    logger.info("Demucs Worker остановлен.")

if __name__ == '__main__':
    main()