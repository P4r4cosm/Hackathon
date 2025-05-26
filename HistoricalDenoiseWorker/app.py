import os
import uuid
import subprocess
import pika
import json
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

RABBITMQ_CONSUME_QUEUE_NAME = os.getenv('RABBITMQ_CONSUME_QUEUE_NAME', 'historical_denoise_tasks_queue')
RABBITMQ_CONSUME_EXCHANGE_NAME = os.getenv('RABBITMQ_CONSUME_EXCHANGE_NAME', 'audio_processing_exchange')
RABBITMQ_CONSUME_ROUTING_KEY = os.getenv('RABBITMQ_CONSUME_ROUTING_KEY', 'historical_denoise.task')

RABBITMQ_PUBLISH_EXCHANGE_NAME = os.getenv('RABBITMQ_PUBLISH_EXCHANGE_NAME', 'results_exchange')
RABBITMQ_PUBLISH_ROUTING_KEY = os.getenv('RABBITMQ_PUBLISH_ROUTING_KEY', 'task.result.historical_denoise')

MINIO_ENDPOINT = os.getenv('MINIO_ENDPOINT', 'localhost:9000')
MINIO_ACCESS_KEY = os.getenv('MINIO_ACCESS_KEY', 'minioadmin')
MINIO_SECRET_KEY = os.getenv('MINIO_SECRET_KEY', 'minioadmin')
MINIO_USE_SSL = os.getenv('MINIO_USE_SSL', 'False').lower() == 'true'
MINIO_DEFAULT_BUCKET = os.getenv('MINIO_BUCKET_NAME', 'audio-bucket')

RECONNECT_DELAY_SECONDS = 5
MAX_RETRIES_RABBITMQ = int(os.getenv('MAX_RETRIES_RABBITMQ', 5))

# --- Инициализация клиента MinIO ---
minio_client = None # по умолчанию
try:
    # Проверяем наличие ключей в переменных окружения перед инициализацией
    minio_access_key_val = os.getenv('MINIO_ACCESS_KEY')
    minio_secret_key_val = os.getenv('MINIO_SECRET_KEY')
    if not minio_access_key_val or not minio_secret_key_val:
        logger.error("Переменные окружения MINIO_ACCESS_KEY и MINIO_SECRET_KEY должны быть установлены.")
    else:
        minio_client = Minio(
            MINIO_ENDPOINT,
            access_key=minio_access_key_val,
            secret_key=minio_secret_key_val,
            secure=MINIO_USE_SSL
        )
        logger.info(f"Клиент MinIO успешно инициализирован для эндпоинта {MINIO_ENDPOINT}")
except Exception as e:
    logger.error(f"Не удалось инициализировать клиент MinIO: {e}")
    # minio_client останется None


def ensure_minio_bucket_exists(bucket_name):
    """Проверяет существование бакета и создает его, если необходимо."""
    if not minio_client:
        logger.error("Клиент MinIO не инициализирован. Невозможно проверить существование бакета.")
        raise ConnectionError("Клиент MinIO не инициализирован")
    try:
        found = minio_client.bucket_exists(bucket_name)
        if not found:
            minio_client.make_bucket(bucket_name)
            logger.info(f"Бакет MinIO '{bucket_name}' создан.")
        else:
            logger.info(f"Бакет MinIO '{bucket_name}' уже существует.")
    except S3Error as e:
        logger.error(f"Ошибка MinIO при проверке или создании бакета {bucket_name}: {e}")
        raise
    except Exception as e_generic:
        logger.error(f"Общая ошибка в ensure_minio_bucket_exists для {bucket_name}: {e_generic}")
        raise


def publish_processing_result(channel, task_id, status, result_data):
    """Отправляет результат обработки обратно в RabbitMQ."""
    message_payload = {
        "task_id": task_id,
        "tool": "historical_denoise",
        "status": status,
        **result_data
    }
    try:
        channel.basic_publish(
            exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME,
            routing_key=RABBITMQ_PUBLISH_ROUTING_KEY, # Можно сделать task.result.historical_denoise
            body=json.dumps(message_payload),
            properties=pika.BasicProperties(
                delivery_mode=pika.spec.PERSISTENT_DELIVERY_MODE,
                content_type='application/json',
                correlation_id=task_id
            )
        )
        logger.info(f"Результат для task_id {task_id} опубликован: tool='historical_denoise', status='{status}'")
    except Exception as e:
        logger.error(f"Не удалось опубликовать результат для task_id {task_id}: {e}")


def run_historical_denoise_process(task_id, input_audio_path_host, base_temp_dir_for_docker_data_on_host):
    """
    Запускает процесс historical-denoise в Docker контейнере historical-denoiser.
    input_audio_path_host - путь к ИСХОДНОМУ файлу на хост-машине воркера (уже скачанному из MinIO).
    base_temp_dir_for_docker_data_on_host - путь к временной директории на хосте, которая будет смонтирована как /app/data в контейнер.
    (Ожидаемая структура внутри base_temp_dir_for_docker_data_on_host: ./input/ и ./output/)
    """
    input_filename = os.path.basename(input_audio_path_host)
    input_filename_stem = os.path.splitext(input_filename)[0]

    # Пути внутри временной директории на хосте
    host_docker_input_dir = os.path.join(base_temp_dir_for_docker_data_on_host, "input")
    host_docker_output_dir = os.path.join(base_temp_dir_for_docker_data_on_host, "output") 
    
    os.makedirs(host_docker_input_dir, exist_ok=True)
    os.makedirs(host_docker_output_dir, exist_ok=True)

    host_input_file_for_docker = os.path.join(host_docker_input_dir, input_filename)
    shutil.copy(input_audio_path_host, host_input_file_for_docker)

    expected_denoised_file_on_host = os.path.join(host_docker_output_dir, input_filename_stem, "denoised.wav")

    inference_script_path_in_container = "/app/inference.py"

    cmd = [
        "docker", "run", "--rm",
        "-v", f"{base_temp_dir_for_docker_data_on_host}:/app/data",
        "historical-denoiser:latest", 
        "python", inference_script_path_in_container
    ]

    logger.info(f"Задача {task_id}: Выполнение Historical Denoise: {' '.join(cmd)}")
    try:
        process = subprocess.run(cmd, capture_output=True, text=True, check=False)
        logger.debug(f"Задача {task_id}: Historical Denoise stdout:\n{process.stdout}")
        if process.stderr:
            logger.warning(f"Задача {task_id}: Historical Denoise stderr:\n{process.stderr}")

        if process.returncode != 0:
            return None, {
                "error_message": "Ошибка обработки Historical Denoise",
                "details": {"stdout": process.stdout, "stderr": process.stderr, "returncode": process.returncode}
            }

        if not os.path.exists(expected_denoised_file_on_host):
            logger.warning(f"Задача {task_id}: Ожидаемый очищенный файл не найден по пути {expected_denoised_file_on_host}.")
            found_denoised_files = []
            output_subdir_for_file = os.path.join(host_docker_output_dir, input_filename_stem)
            if os.path.exists(output_subdir_for_file):
                for f_name in os.listdir(output_subdir_for_file):
                    if "denoised.wav" in f_name.lower():
                        found_denoised_files.append(os.path.join(output_subdir_for_file, f_name))
            
            if found_denoised_files:
                logger.info(f"Задача {task_id}: Найден очищенный файл: {found_denoised_files[0]}. Используется этот файл.")
                return found_denoised_files[0], None
            
            return None, {
                "error_message": f"Выходной файл Historical Denoise 'denoised.wav' не найден в {output_subdir_for_file}",
                "details": {"search_path": output_subdir_for_file, "stdout": process.stdout, "stderr": process.stderr }
            }

        logger.info(f"Задача {task_id}: Обработка Historical Denoise успешна. Очищенное аудио по пути {expected_denoised_file_on_host}")
        return expected_denoised_file_on_host, None

    except Exception as e:
        logger.exception(f"Задача {task_id}: Исключение во время выполнения Historical Denoise: {e}")
        return None, {"error_message": f"Исключение при выполнении Historical Denoise: {str(e)}"}
    finally:
        if os.path.exists(host_input_file_for_docker):
            try:
                os.remove(host_input_file_for_docker)
            except OSError as e_remove:
                logger.warning(f"Не удалось удалить временный входной файл для Docker {host_input_file_for_docker}: {e_remove}")


def process_single_task(task_id, input_bucket, input_object_name, output_file_basename):
    """Полный цикл обработки одной задачи: скачать, обработать, загрузить."""
    if not minio_client:
        logger.error(f"Задача {task_id}: Клиент MinIO недоступен. Невозможно обработать задачу.")
        return {"error_message": "Клиент MinIO недоступен. Ошибка конфигурации воркера."}

    with tempfile.TemporaryDirectory(prefix="hd_worker_docker_mount_") as host_temp_docker_data_dir:
        with tempfile.NamedTemporaryFile(delete=False, prefix=f"{task_id}_", suffix=os.path.splitext(input_object_name)[1]) as temp_downloaded_file_on_host:
            host_local_downloaded_path = temp_downloaded_file_on_host.name
        
        try:
            logger.info(f"Задача {task_id}: Загрузка s3://{input_bucket}/{input_object_name} в {host_local_downloaded_path}")
            minio_client.fget_object(input_bucket, input_object_name, host_local_downloaded_path)

            denoised_file_path_on_host, processing_error = run_historical_denoise_process(
                task_id,
                host_local_downloaded_path,
                host_temp_docker_data_dir
            )
            
            if processing_error:
                return processing_error

            minio_output_object_name = f"results/historical_denoise/{task_id}_{output_file_basename}_denoised.wav"
            logger.info(f"Задача {task_id}: Загрузка {denoised_file_path_on_host} в s3://{input_bucket}/{minio_output_object_name}")
            minio_client.fput_object(
                input_bucket,
                minio_output_object_name,
                denoised_file_path_on_host, 
                content_type='audio/wav'
            )
            logger.info(f"Задача {task_id}: Результат успешно загружен в MinIO.")
            return {
                "output_bucket_name": input_bucket,
                "output_object_name": minio_output_object_name,
                "message": "Обработка Historical Denoise успешно завершена."
            }

        except S3Error as e: 
            logger.error(f"Задача {task_id}: Ошибка операции MinIO: {e}")
            return {"error_message": f"Ошибка операции MinIO: {str(e)}", "details": {"bucket": input_bucket, "object": input_object_name}}
        except Exception as e: 
            logger.exception(f"Задача {task_id}: Необработанное исключение в process_single_task: {e}")
            return {"error_message": f"Необработанное исключение в process_single_task: {str(e)}"}
        finally:
            if os.path.exists(host_local_downloaded_path):
                 try: os.remove(host_local_downloaded_path)
                 except OSError as e_remove: logger.warning(f"Не удалось удалить временный загруженный файл {host_local_downloaded_path}: {e_remove}")


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

        logger.info(f"Задача {task_id_from_msg}: Обработка s3://{input_bucket}/{input_object} для historical denoise.")
        
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
        publish_processing_result(channel, task_id_from_msg, "error", {"error_message": f"Необработанное исключение воркера: {str(e)}"})
        channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)


def main():
    logger.info("Historical Denoise Worker запускается...")

    # Глобальная переменная minio_client инициализируется в начале файла.
    if not minio_client:
        logger.warning("Клиент MinIO не был инициализирован при запуске. Проверьте переменные окружения MINIO_ACCESS_KEY/MINIO_SECRET_KEY и доступность сервера MinIO. Воркер попытается продолжить работу, но операции MinIO завершатся ошибкой.")

    connection = None
    retries = 0
    while retries < MAX_RETRIES_RABBITMQ or MAX_RETRIES_RABBITMQ == 0:
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
            retries = 0 

            channel.exchange_declare(exchange=RABBITMQ_CONSUME_EXCHANGE_NAME, exchange_type='direct', durable=True)
            channel.queue_declare(queue=RABBITMQ_CONSUME_QUEUE_NAME, durable=True)
            channel.queue_bind(
                exchange=RABBITMQ_CONSUME_EXCHANGE_NAME,
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                routing_key=RABBITMQ_CONSUME_ROUTING_KEY
            )

            channel.exchange_declare(exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME, exchange_type='direct', durable=True)
            
            # --- БЛОК ПРОВЕРКИ MINIO (после RabbitMQ setup) ---
            try:
                logger.info(f"Проверка существования бакета MinIO '{MINIO_DEFAULT_BUCKET}' (после настройки RabbitMQ).")
                ensure_minio_bucket_exists(MINIO_DEFAULT_BUCKET)
                logger.info(f"Проверка бакета MinIO '{MINIO_DEFAULT_BUCKET}' успешна.")
            except ConnectionError as e_minio_conn:
                 logger.error(f"Клиент MinIO не инициализирован, невозможно проверить бакет: {e_minio_conn}")
            except Exception as e_minio_bucket:
                logger.error(f"Ошибка при проверке существования бакета MinIO '{MINIO_DEFAULT_BUCKET}': {e_minio_bucket}. Воркер может некорректно обрабатывать задачи.")

            channel.basic_qos(prefetch_count=1)
            channel.basic_consume(
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                on_message_callback=on_message_callback
            )

            logger.info(f"[*] Ожидание задач в очереди '{RABBITMQ_CONSUME_QUEUE_NAME}'. Для выхода нажмите CTRL+C")
            channel.start_consuming()

        except pika.exceptions.AMQPConnectionError as e:
            logger.error(f"Ошибка подключения/канала RabbitMQ: {e}")
            if connection and not connection.is_closed:
                try: connection.close()
                except: pass 
            retries += 1
            if MAX_RETRIES_RABBITMQ > 0 and retries >= MAX_RETRIES_RABBITMQ:
                logger.error(f"Достигнуто максимальное количество попыток подключения к RabbitMQ ({MAX_RETRIES_RABBITMQ}). Выход.")
                break
            logger.info(f"Повторная попытка через {RECONNECT_DELAY_SECONDS} секунд...")
            time.sleep(RECONNECT_DELAY_SECONDS)
        except KeyboardInterrupt:
            logger.info("Historical Denoise Worker корректно завершает работу по прерыванию пользователя...")
            if connection and channel and channel.is_open:
                channel.stop_consuming()
            if connection and connection.is_open:
                connection.close()
            logger.info("Соединение RabbitMQ закрыто.")
            break
        except Exception as e: 
            logger.exception(f"Произошла неожиданная критическая ошибка в основном цикле: {e}")
            if connection and not connection.is_closed:
                try: connection.close()
                except: pass
            retries += 1 
            if MAX_RETRIES_RABBITMQ > 0 and retries >= MAX_RETRIES_RABBITMQ:
                logger.error(f"Достигнуто максимальное количество попыток после критической ошибки. Выход.")
                break
            logger.info(f"Повторная попытка через {RECONNECT_DELAY_SECONDS * 2} секунд после критической ошибки...")
            time.sleep(RECONNECT_DELAY_SECONDS * 2)

    logger.info("Historical Denoise Worker остановлен.")


if __name__ == '__main__':
    main() 