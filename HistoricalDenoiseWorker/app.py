import os
import uuid
import subprocess
import pika
import json
import shutil # Keep for tempfile.TemporaryDirectory, even if not used explicitly for copying
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

RABBITMQ_CONSUME_QUEUE_NAME = os.getenv('RABBITMQ_DENOISE_QUEUE_NAME', 'historical_denoise_tasks_queue') # Очередь, которую слушаем
RABBITMQ_CONSUME_EXCHANGE_NAME = os.getenv('RABBITMQ_AUDIO_PROCESSING_EXCHANGE', 'audio_processing_exchange') # Exchange, к которому привязана очередь
RABBITMQ_CONSUME_ROUTING_KEY = os.getenv('RABBITMQ_DENOISE_ROUTING_KEY', 'historical_denoise.task') # Ключ для привязки

RABBITMQ_PUBLISH_EXCHANGE_NAME = os.getenv('RABBITMQ_RESULTS_EXCHANGE_NAME', 'results_exchange') # Exchange для результатов
RABBITMQ_PUBLISH_ROUTING_KEY = os.getenv('RABBITMQ_TASK_RESULTS_ROUTING_KEY', 'task.result') # Ключ для результатов (можно сделать специфичнее, если нужно)

MINIO_ENDPOINT = os.getenv('MINIO_ENDPOINT', 'localhost:9000')
MINIO_ACCESS_KEY = os.getenv('MINIO_ACCESS_KEY')
MINIO_SECRET_KEY = os.getenv('MINIO_SECRET_KEY')
MINIO_USE_SSL = os.getenv('MINIO_USE_SSL', 'False').lower() == 'true'
MINIO_DEFAULT_BUCKET = os.getenv('MINIO_BUCKET_NAME', 'audio-bucket')

# Параметры для historical-denoise (если нужны специфичные, которые не задаются через Hydra conf)
# Например, HISTORICAL_DENOISE_MODEL_PATH = os.getenv('HISTORICAL_DENOISE_MODEL_PATH', '/app/experiments/trained_model/checkpoint')
# Но inference.py уже знает где модель из его conf

RECONNECT_DELAY_SECONDS = 5
MAX_RETRIES_RABBITMQ = int(os.getenv('MAX_RETRIES_RABBITMQ', 5))

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
        found = minio_client.bucket_exists(bucket_name)
        if not found:
            minio_client.make_bucket(bucket_name)
            logger.info(f"MinIO bucket '{bucket_name}' created.")
        else:
            logger.info(f"MinIO bucket '{bucket_name}' already exists.")
    except S3Error as e:
        logger.error(f"MinIO error checking or creating bucket {bucket_name}: {e}")
        raise


def publish_processing_result(channel, task_id, status, result_data):
    """Отправляет результат обработки обратно в RabbitMQ."""
    message_payload = {
        "task_id": task_id,
        "tool": "historical_denoise", # Добавим, какой инструмент отработал
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
        logger.info(f"Published result for task_id {task_id}: tool='historical_denoise', status='{status}'")
    except Exception as e:
        logger.error(f"Failed to publish result for task_id {task_id}: {e}")


def run_historical_denoise_process(task_id, input_audio_path_host, base_temp_dir_for_docker_data_on_host):
    """
    Запускает процесс historical-denoise в Docker контейнере historical-denoiser.
    input_audio_path_host - путь к ИСХОДНОМУ файлу на хост-машине воркера (уже скачанному из MinIO).
    base_temp_dir_for_docker_data_on_host - путь к временной директории на хосте, которая будет смонтирована как /app/data в контейнер.
    Структура внутри base_temp_dir_for_docker_data_on_host:
        ./input/  (сюда копируется input_audio_path_host)
        ./output/ (здесь historical-denoiser создаст подпапки с результатами)
    """
    input_filename = os.path.basename(input_audio_path_host)
    input_filename_stem = os.path.splitext(input_filename)[0]

    # Пути внутри временной директории на хосте
    host_docker_input_dir = os.path.join(base_temp_dir_for_docker_data_on_host, "input")
    host_docker_output_dir = os.path.join(base_temp_dir_for_docker_data_on_host, "output") # historical-denoiser создаст здесь подпапки
    
    os.makedirs(host_docker_input_dir, exist_ok=True)
    os.makedirs(host_docker_output_dir, exist_ok=True) # Создаем, чтобы Docker мог в нее писать, если inference.py не создаст ее сам

    # Копируем исходный файл в подготовленную input директорию для Docker
    host_input_file_for_docker = os.path.join(host_docker_input_dir, input_filename)
    shutil.copy(input_audio_path_host, host_input_file_for_docker)

    # Путь к ожидаемому результату на хосте после работы контейнера
    expected_denoised_file_on_host = os.path.join(host_docker_output_dir, input_filename_stem, "denoised.wav")

    # Путь к inference.py внутри образа historical-denoiser (обычно /app/inference.py)
    inference_script_path_in_container = "/app/inference.py"
    # Рабочая директория внутри контейнера historical-denoiser обычно /app/

    cmd = [
        "docker", "run", "--rm",
        "--gpus", "all", # Если доступен GPU
        "-v", f"{base_temp_dir_for_docker_data_on_host}:/app/data", # Монтируем всю временную папку как /app/data
        "historical-denoiser:latest", # Имя Docker-образа
        "python", inference_script_path_in_container # Hydra параметры больше не нужны, скрипт работает с /app/data/input и /app/data/output
    ]

    logger.info(f"Task {task_id}: Executing Historical Denoise: {' '.join(cmd)}")
    try:
        process = subprocess.run(cmd, capture_output=True, text=True, check=False)
        logger.debug(f"Task {task_id}: Historical Denoise stdout:\n{process.stdout}")
        if process.stderr:
            logger.warning(f"Task {task_id}: Historical Denoise stderr:\n{process.stderr}")

        if process.returncode != 0:
            return None, {
                "error_message": "Historical Denoise processing failed",
                "details": {"stdout": process.stdout, "stderr": process.stderr, "returncode": process.returncode}
            }

        if not os.path.exists(expected_denoised_file_on_host):
            logger.warning(f"Task {task_id}: Expected denoised file not found at {expected_denoised_file_on_host}.")
            # Дополнительный поиск, если структура вывода немного отличается или имя файла другое
            found_denoised_files = []
            output_subdir_for_file = os.path.join(host_docker_output_dir, input_filename_stem)
            if os.path.exists(output_subdir_for_file):
                for f_name in os.listdir(output_subdir_for_file):
                    if "denoised.wav" in f_name.lower(): # Ищем файл, содержащий denoised.wav
                        found_denoised_files.append(os.path.join(output_subdir_for_file, f_name))
            
            if found_denoised_files:
                logger.info(f"Task {task_id}: Found denoised file at: {found_denoised_files[0]}. Using this file.")
                return found_denoised_files[0], None
            
            return None, {
                "error_message": f"Historical Denoise output file 'denoised.wav' not found in {output_subdir_for_file}",
                "details": {"search_path": output_subdir_for_file, "stdout": process.stdout, "stderr": process.stderr }
            }

        logger.info(f"Task {task_id}: Historical Denoise processing successful. Denoised audio at {expected_denoised_file_on_host}")
        return expected_denoised_file_on_host, None

    except Exception as e:
        logger.exception(f"Task {task_id}: Exception during Historical Denoise execution: {e}")
        return None, {"error_message": f"Historical Denoise execution exception: {str(e)}"}
    finally:
        # Очищаем скопированный входной файл в base_temp_dir_for_docker_data_on_host/input/
        # Сама base_temp_dir_for_docker_data_on_host будет удалена менеджером контекста TemporaryDirectory
        if os.path.exists(host_input_file_for_docker):
            try:
                os.remove(host_input_file_for_docker)
            except OSError as e_remove:
                logger.warning(f"Could not remove temp input file for docker {host_input_file_for_docker}: {e_remove}")


def process_single_task(task_id, input_bucket, input_object_name, output_file_basename):
    """Полный цикл обработки одной задачи: скачать, обработать, загрузить."""
    # Создаем одну временную директорию на хосте, которая будет смонтирована как /app/data в Docker
    with tempfile.TemporaryDirectory(prefix="hd_worker_docker_mount_") as host_temp_docker_data_dir:
        
        # Внутри этой директории будет input/ и output/
        # Файл из MinIO скачивается во временное место на хосте (может быть и вне host_temp_docker_data_dir,
        # но для простоты сначала скачаем его, а потом скопируем в host_temp_docker_data_dir/input/)
        
        # Создаем временный файл на хосте для скачивания из MinIO (вне директории монтирования Docker, чтобы избежать путаницы)
        with tempfile.NamedTemporaryFile(delete=False, prefix=f"{task_id}_", suffix=os.path.splitext(input_object_name)[1]) as temp_downloaded_file_on_host:
            host_local_downloaded_path = temp_downloaded_file_on_host.name
        
        try:
            # 1. Скачать файл из MinIO во временный файл на хосте
            logger.info(f"Task {task_id}: Downloading s3://{input_bucket}/{input_object_name} to {host_local_downloaded_path}")
            minio_client.fget_object(input_bucket, input_object_name, host_local_downloaded_path)

            # 2. Запустить Historical Denoise
            #    run_historical_denoise_process копирует host_local_downloaded_path в host_temp_docker_data_dir/input/
            #    и ожидает результат в host_temp_docker_data_dir/output/
            denoised_file_path_on_host, processing_error = run_historical_denoise_process(
                task_id,
                host_local_downloaded_path,         # Путь к скачанному файлу на хосте
                host_temp_docker_data_dir           # Базовая директория на хосте для Docker data (/app/data)
            )
            
            if processing_error:
                return processing_error

            # 3. Загрузить результат в MinIO
            minio_output_object_name = f"results/historical_denoise/{task_id}_{output_file_basename}_denoised.wav"
            logger.info(f"Task {task_id}: Uploading {denoised_file_path_on_host} to s3://{input_bucket}/{minio_output_object_name}")
            minio_client.fput_object(
                input_bucket,
                minio_output_object_name,
                denoised_file_path_on_host, # Это путь к файлу в host_temp_docker_data_dir/output/...
                content_type='audio/wav'
            )
            logger.info(f"Task {task_id}: Successfully uploaded result to MinIO.")
            return {
                "output_bucket_name": input_bucket,
                "output_object_name": minio_output_object_name,
                "message": "Historical Denoise processing completed successfully."
            }

        except S3Error as e:
            logger.error(f"Task {task_id}: MinIO operation failed: {e}")
            return {"error_message": f"MinIO operation error: {str(e)}", "details": {"bucket": input_bucket, "object": input_object_name}}
        except Exception as e: # Любые другие ошибки (включая ошибки из run_historical_denoise_process, если они не перехвачены там как dict)
            logger.exception(f"Task {task_id}: Unhandled exception in process_single_task: {e}")
            return {"error_message": f"Unhandled exception in process_single_task: {str(e)}"}
        finally:
            # Очистка временного скачанного файла
            if os.path.exists(host_local_downloaded_path):
                 try: os.remove(host_local_downloaded_path)
                 except OSError as e_remove: logger.warning(f"Could not remove temp downloaded file {host_local_downloaded_path}: {e_remove}")
            # host_temp_docker_data_dir будет удалена автоматически


def on_message_callback(channel, method_frame, properties, body):
    """Обработчик входящего сообщения от RabbitMQ."""
    task_id_from_msg = "unknown_task"
    try:
        message_str = body.decode('utf-8')
        logger.info(f"Received raw message (delivery_tag={method_frame.delivery_tag}): {message_str}")
        task_data = json.loads(message_str)

        task_id_from_msg = task_data.get("task_id") or task_data.get("TaskId") or str(uuid.uuid4())
        input_bucket = task_data.get("input_bucket_name") or MINIO_DEFAULT_BUCKET
        input_object = task_data.get("input_object_name") or task_data.get("MinioFilePath")
        output_basename = task_data.get("output_file_basename") or \
                          (os.path.splitext(os.path.basename(input_object))[0] if input_object else task_id_from_msg)

        if not input_object:
            logger.error(f"Task {task_id_from_msg}: Missing 'input_object_name' or 'MinioFilePath' in message.")
            publish_processing_result(channel, task_id_from_msg, "error",
                                      {"error_message": "Invalid task: missing input object path."})
            channel.basic_ack(delivery_tag=method_frame.delivery_tag)
            return

        logger.info(f"Task {task_id_from_msg}: Processing s3://{input_bucket}/{input_object} for historical denoise.")
        
        result_payload = process_single_task(task_id_from_msg, input_bucket, input_object, output_basename)

        if "error_message" in result_payload:
            publish_processing_result(channel, task_id_from_msg, "error", result_payload)
        else:
            publish_processing_result(channel, task_id_from_msg, "success", result_payload)

        channel.basic_ack(delivery_tag=method_frame.delivery_tag)
        logger.info(f"Task {task_id_from_msg} (delivery_tag={method_frame.delivery_tag}) acknowledged.")

    except json.JSONDecodeError as e:
        logger.error(f"Failed to decode JSON message body: {body[:200]}... Error: {e}")
        channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)
    except Exception as e:
        logger.exception(f"Unhandled exception while processing task {task_id_from_msg} (delivery_tag={method_frame.delivery_tag}): {e}")
        publish_processing_result(channel, task_id_from_msg, "error", {"error_message": f"Unhandled worker exception: {str(e)}"})
        channel.basic_nack(delivery_tag=method_frame.delivery_tag, requeue=False)


def main():
    logger.info("Historical Denoise Worker starting...")

    try:
        ensure_minio_bucket_exists(MINIO_DEFAULT_BUCKET)
    except Exception:
        logger.error(f"Critical error ensuring MinIO bucket '{MINIO_DEFAULT_BUCKET}' exists. Worker cannot start.")
        return

    connection = None
    retries = 0
    while retries < MAX_RETRIES_RABBITMQ or MAX_RETRIES_RABBITMQ == 0:
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
            retries = 0 

            channel.exchange_declare(exchange=RABBITMQ_CONSUME_EXCHANGE_NAME, exchange_type='direct', durable=True)
            channel.queue_declare(queue=RABBITMQ_CONSUME_QUEUE_NAME, durable=True)
            channel.queue_bind(
                exchange=RABBITMQ_CONSUME_EXCHANGE_NAME,
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                routing_key=RABBITMQ_CONSUME_ROUTING_KEY
            )

            channel.exchange_declare(exchange=RABBITMQ_PUBLISH_EXCHANGE_NAME, exchange_type='direct', durable=True)
            
            channel.basic_qos(prefetch_count=1)
            channel.basic_consume(
                queue=RABBITMQ_CONSUME_QUEUE_NAME,
                on_message_callback=on_message_callback
            )

            logger.info(f"[*] Waiting for tasks on queue '{RABBITMQ_CONSUME_QUEUE_NAME}'. To exit press CTRL+C")
            channel.start_consuming()

        except pika.exceptions.AMQPConnectionError as e:
            logger.error(f"RabbitMQ connection/channel error: {e}")
            if connection and not connection.is_closed:
                try: connection.close()
                except: pass 
            retries += 1
            if MAX_RETRIES_RABBITMQ > 0 and retries >= MAX_RETRIES_RABBITMQ:
                logger.error(f"Max RabbitMQ connection retries ({MAX_RETRIES_RABBITMQ}) reached. Exiting.")
                break
            logger.info(f"Retrying in {RECONNECT_DELAY_SECONDS} seconds...")
            time.sleep(RECONNECT_DELAY_SECONDS)
        except KeyboardInterrupt:
            logger.info("Historical Denoise Worker shutting down gracefully by user interrupt...")
            if connection and channel and channel.is_open:
                channel.stop_consuming()
            if connection and connection.is_open:
                connection.close()
            logger.info("RabbitMQ connection closed.")
            break
        except Exception as e: 
            logger.exception(f"An unexpected critical error occurred in main loop: {e}")
            if connection and not connection.is_closed:
                try: connection.close()
                except: pass
            retries += 1 
            if MAX_RETRIES_RABBITMQ > 0 and retries >= MAX_RETRIES_RABBITMQ:
                logger.error(f"Max retries reached after critical error. Exiting.")
                break
            logger.info(f"Retrying in {RECONNECT_DELAY_SECONDS * 2} seconds after critical error...")
            time.sleep(RECONNECT_DELAY_SECONDS * 2)

    logger.info("Historical Denoise Worker stopped.")


if __name__ == '__main__':
    main() 