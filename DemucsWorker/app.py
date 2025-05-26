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

RABBITMQ_CONSUME_QUEUE_NAME = os.getenv('RABBITMQ_CONSUME_QUEUE_NAME', 'demucs_tasks_queue')
RABBITMQ_CONSUME_EXCHANGE_NAME = os.getenv('RABBITMQ_CONSUME_EXCHANGE_NAME', 'audio_processing_exchange')
RABBITMQ_CONSUME_ROUTING_KEY = os.getenv('RABBITMQ_CONSUME_ROUTING_KEY', 'demucs.task')

RABBITMQ_PUBLISH_EXCHANGE_NAME = os.getenv('RABBITMQ_PUBLISH_EXCHANGE_NAME', 'results_exchange')
RABBITMQ_PUBLISH_ROUTING_KEY = os.getenv('RABBITMQ_PUBLISH_ROUTING_KEY', 'task.result.demucs')

MINIO_ENDPOINT = os.getenv('MINIO_ENDPOINT', 'localhost:9000')
MINIO_ACCESS_KEY = os.getenv('MINIO_ACCESS_KEY')
MINIO_SECRET_KEY = os.getenv('MINIO_SECRET_KEY')
MINIO_USE_SSL = os.getenv('MINIO_USE_SSL', 'False').lower() == 'true'
MINIO_DEFAULT_BUCKET = os.getenv('MINIO_BUCKET_NAME', 'audio-bucket')

DEMUCS_MODEL = os.getenv('DEMUCS_MODEL', "htdemucs")
DEMUCS_SHIFTS = int(os.getenv('DEMUCS_SHIFTS', 0))
DEMUCS_DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

RECONNECT_DELAY_SECONDS = 5
MAX_RETRIES_RABBITMQ = int(os.getenv('MAX_RETRIES_RABBITMQ', 5))

# --- Инициализация клиента MinIO ---
minio_client = None
try:
    if not MINIO_ACCESS_KEY or not MINIO_SECRET_KEY:
        logger.error("Переменные окружения MINIO_ACCESS_KEY и MINIO_SECRET_KEY должны быть установлены.")
        exit(1)
    minio_client = Minio(
        MINIO_ENDPOINT,
        access_key=MINIO_ACCESS_KEY,
        secret_key=MINIO_SECRET_KEY,
        secure=MINIO_USE_SSL
    )
    logger.info(f"Клиент MinIO успешно инициализирован для эндпоинта {MINIO_ENDPOINT}")
except Exception as e:
    logger.error(f"Не удалось инициализировать клиент MinIO: {e}")
    exit(1)


def ensure_minio_bucket_exists(bucket_name):
    """Проверяет существование бакета и создает его, если необходимо."""
    try:
        buckets = minio_client.list_buckets()
        for b in buckets:
            logger.info(f"Найден бакет: {b.name}")
        logger.info(f"Список бакетов успешно получен. Проверка наличия '{bucket_name}'...")

        found = minio_client.bucket_exists(bucket_name)
        if not found:
            minio_client.make_bucket(bucket_name)
            logger.info(f"Бакет MinIO '{bucket_name}' создан.")
        else:
            logger.info(f"Бакет MinIO '{bucket_name}' уже существует.")
    except S3Error as e:
        logger.error(f"Ошибка MinIO при проверке или создании бакета {bucket_name}: {e}")
        raise # Перебрасываем, чтобы обработать выше


def publish_processing_result(channel, task_id, status, result_data):
    """Отправляет результат обработки обратно в RabbitMQ."""
    message_payload = {
        "task_id": task_id,
        "tool": "demucs", # Добавим для ясности, какой воркер отработал
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
        logger.info(f"Результат для task_id {task_id} опубликован: tool='demucs', status='{status}'")
    except Exception as e:
        logger.error(f"Не удалось опубликовать результат для task_id {task_id}: {e}")
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
    """Полный цикл обработки одной задачи: скачать, Demucs, загрузить."""
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
        try:
            minio_client.fget_object(input_bucket, input_object_name, local_input_path)
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
            minio_client.fput_object(
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


def on_message_callback(channel, method_frame, properties, body):
    """Обработчик входящего сообщения от RabbitMQ."""
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
    except Exception as e:
        logger.exception(f"Необработанное исключение при обработке задачи {task_id_from_msg} (delivery_tag={method_frame.delivery_tag}): {e}")
        # Попытка отправить сообщение об ошибке, если это возможно
        try:
            error_msg_payload = {
                "task_id": task_id_from_msg,
                "status": "error",
                "error_message": f"Необработанное исключение воркера: {str(e)}"
            }
            publish_processing_result(channel, task_id_from_msg, "error", error_msg_payload)
        except Exception as pub_e:
            logger.error(f"Не удалось опубликовать критическое сообщение об ошибке для задачи {task_id_from_msg}: {pub_e}")
        channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False) # Не переотправлять после серьезной ошибки


def main():
    logger.info(f"Demucs Worker запускается... Устройство: {DEMUCS_DEVICE}")

    try:
        ensure_minio_bucket_exists(MINIO_DEFAULT_BUCKET) # Проверка/создание бакета при старте
    except Exception as e_minio_init:
        logger.error(f"Критическая ошибка при проверке/создании бакета MinIO '{MINIO_DEFAULT_BUCKET}': {e_minio_init}. Воркер не может запуститься.")
        return # Выход, если MinIO недоступен или бакет не может быть создан

    connection = None
    retries = 0
    while retries < MAX_RETRIES_RABBITMQ or MAX_RETRIES_RABBITMQ == 0: # 0 для бесконечных попыток
        try:
            logger.info(f"Попытка подключения к RabbitMQ: {RABBITMQ_HOST}:{RABBITMQ_PORT} (Попытка {retries + 1})")
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
            retries = 0 # Сброс счетчика при успешном подключении

            channel.exchange_declare(exchange=RABBITMQ_CONSUME_EXCHANGE_NAME, exchange_type='direct', durable=True)
            channel.queue_declare(queue=RABBITMQ_CONSUME_QUEUE_NAME, durable=True)
            channel.queue_bind(
                exchange=RABBITMQ_CONSUME_EXCHANGE_NAME,
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                routing_key=RABBITMQ_CONSUME_ROUTING_KEY
            )
            channel.exchange_declare(exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME, exchange_type='direct', durable=True)
            
            channel.basic_qos(prefetch_count=1) # Обрабатывать по одному сообщению за раз
            channel.basic_consume(
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                on_message_callback=on_message_callback
            )

            logger.info(f"[*] Ожидание задач в очереди '{RABBITMQ_CONSUME_QUEUE_NAME}'. Для выхода нажмите CTRL+C")
            channel.start_consuming()
            # Если start_consuming завершился штатно (например, KeyboardInterrupt в callback или stop_consuming)
            break # Выход из цикла while

        except pika.exceptions.AMQPConnectionError as e:
            logger.error(f"Ошибка подключения/канала RabbitMQ: {e}")
            if connection and not connection.is_closed:
                try: connection.close()
                except: pass # Игнорируем ошибки при закрытии проблемного соединения
            retries += 1
            if MAX_RETRIES_RABBITMQ > 0 and retries >= MAX_RETRIES_RABBITMQ:
                logger.error(f"Достигнуто максимальное количество попыток подключения к RabbitMQ ({MAX_RETRIES_RABBITMQ}). Выход.")
                break # Выход из цикла while
            logger.info(f"Повторная попытка через {RECONNECT_DELAY_SECONDS} секунд...")
            time.sleep(RECONNECT_DELAY_SECONDS)
        except KeyboardInterrupt: # Обработка Ctrl+C в главном цикле
            logger.info("Demucs Worker корректно завершает работу по прерыванию пользователя...")
            if connection and channel and channel.is_open:
                channel.stop_consuming()
            # Закрытие соединения будет в finally
            break # Выход из цикла while
        except Exception as e: # Другие неожиданные ошибки в главном цикле
            logger.exception(f"Произошла неожиданная критическая ошибка в основном цикле: {e}")
            # Попытка закрыть соединение, если оно есть
            if connection and not connection.is_closed:
                try: connection.close()
                except: pass
            retries += 1 
            if MAX_RETRIES_RABBITMQ > 0 and retries >= MAX_RETRIES_RABBITMQ:
                logger.error(f"Достигнуто максимальное количество попыток после критической ошибки. Выход.")
                break # Выход из цикла while
            logger.info(f"Повторная попытка через {RECONNECT_DELAY_SECONDS * 2} секунд после критической ошибки...")
            time.sleep(RECONNECT_DELAY_SECONDS * 2) # Увеличенная задержка после серьезной ошибки
        finally:
            if connection and connection.is_open:
                logger.info("Закрытие соединения RabbitMQ в блоке finally...")
                try: connection.close()
                except Exception as e_close:
                    logger.error(f"Ошибка при закрытии соединения RabbitMQ: {e_close}")
            logger.info("Соединение RabbitMQ закрыто или не было открыто.")

    logger.info("Demucs Worker остановлен.")


if __name__ == '__main__':
    main()