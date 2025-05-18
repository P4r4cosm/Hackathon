import os
import subprocess
import uuid # Для уникальных имен временных контейнеров
import time # Для небольшой задержки
from flask import Flask, request, send_from_directory, jsonify
from werkzeug.utils import secure_filename
import shutil

app = Flask(__name__)

ALLOWED_EXTENSIONS = {'wav', 'flac'}

# Имя Docker-образа Demucs, который мы собираем через docker-compose
DEMUCS_IMAGE_NAME = "local/demucs_app"
# Имя именованного тома Docker для кэша моделей Demucs
DEMUCS_MODELS_VOLUME = "demucs_models_cache"
# Имя именованного тома Docker для общих аудиоданных
SHARED_AUDIO_VOLUME = "shared_audio_data"

# --- ИЗМЕНЕНИЯ ДЛЯ BIND MOUNT --- 
# Абсолютный путь на ХОСТЕ WINDOWS к общей папке
HOST_SHARED_DIR_PATH = "E:/hacaton/git/Hackathon/shared_docker_data" # Используйте / вместо \ для совместимости
# Путь внутри FlaskServise контейнера, куда монтируется HOST_SHARED_DIR_PATH
FLASK_CONTAINER_MOUNT_POINT_FOR_HOST_SHARED_DIR = "/shared_host_mount"

BASE_SHARED_DIR_FLASK = FLASK_CONTAINER_MOUNT_POINT_FOR_HOST_SHARED_DIR
FLASK_INPUT_DIR = os.path.join(BASE_SHARED_DIR_FLASK, "input_files")
FLASK_OUTPUT_DIR_BASE = os.path.join(BASE_SHARED_DIR_FLASK, "output_files")

# Пути внутри временного контейнера Demucs
DEMUCS_CONTAINER_MOUNT_POINT_FOR_SHARED_DIR = "/data_mount" # Куда будем монтировать HOST_SHARED_DIR_PATH
DEMUCS_CMD_INPUT_DIR_ABS = os.path.join(DEMUCS_CONTAINER_MOUNT_POINT_FOR_SHARED_DIR, "input_files")
DEMUCS_CMD_OUTPUT_DIR_ABS = os.path.join(DEMUCS_CONTAINER_MOUNT_POINT_FOR_SHARED_DIR, "output_files")
# --- КОНЕЦ ИЗМЕНЕНИЙ ДЛЯ BIND MOUNT ---

# Проверка расширения файла
def allowed_file(filename):
    return '.' in filename and \
           filename.rsplit('.', 1)[1].lower() in ALLOWED_EXTENSIONS

@app.route('/process_song', methods=['POST'])
def process_song():
    if 'file' not in request.files:
        return jsonify({'error': 'Файл не найден в запросе'}), 400
    file = request.files['file']
    if file.filename == '':
        return jsonify({'error': 'Файл не выбран'}), 400
    
    if file and allowed_file(file.filename):
        filename = secure_filename(file.filename)
        track_name_without_ext = os.path.splitext(filename)[0]

        # Создаем директории на хосте через путь, видимый FlaskServise
        os.makedirs(FLASK_INPUT_DIR, exist_ok=True)
        os.makedirs(FLASK_OUTPUT_DIR_BASE, exist_ok=True)
        
        temp_input_path_flask = os.path.join(FLASK_INPUT_DIR, filename)
        file.save(temp_input_path_flask)
        
        time.sleep(0.5) # Оставляем на всякий случай

        model_name = "htdemucs_ft"
        demucs_input_file_arg_in_container = os.path.join(DEMUCS_CMD_INPUT_DIR_ABS, filename)

        # Формируем команду для Demucs (ls + demucs)
        demucs_command_str = f"ls -lha {DEMUCS_CMD_INPUT_DIR_ABS} && echo '--- Above is ls, now trying demucs ---' && python3 -m demucs -n {model_name} --two-stems vocals --shifts 1 --overlap 0.3 --out {DEMUCS_CMD_OUTPUT_DIR_ABS} {demucs_input_file_arg_in_container}"
        
        temp_container_name = f"demucs_worker_{uuid.uuid4().hex[:8]}"

        docker_run_command = [
            "docker", "run", "--rm",
            "--name", temp_container_name,
            # Монтируем АБСОЛЮТНЫЙ ПУТЬ С ХОСТА в контейнер Demucs
            "-v", f"{HOST_SHARED_DIR_PATH}:{DEMUCS_CONTAINER_MOUNT_POINT_FOR_SHARED_DIR}", 
            "-v", f"{DEMUCS_MODELS_VOLUME}:/data/models",
            DEMUCS_IMAGE_NAME,
            "sh", "-c", demucs_command_str # Передаем команду через sh -c
        ]
        
        try:
            app.logger.info(f"Executing Demucs via docker run: {' '.join(docker_run_command)}")
            process = subprocess.run(docker_run_command, check=True, capture_output=True, text=True)
            app.logger.info(f"Demucs (via docker run) stdout: {process.stdout}")
            if process.stderr:
                 app.logger.warning(f"Demucs (via docker run) stderr: {process.stderr}")
            
            expected_vocals_flask_path = os.path.join(
                FLASK_OUTPUT_DIR_BASE, 
                model_name, 
                track_name_without_ext, 
                "vocals.wav"
            )

            if os.path.exists(expected_vocals_flask_path):
                return send_from_directory(
                    directory=os.path.dirname(expected_vocals_flask_path), 
                    path="vocals.wav",
                    as_attachment=True
                )
            else:
                error_detail = f"Обработанный файл с вокалом не найден по пути {expected_vocals_flask_path}."
                if process.stdout:
                    error_detail += f" Стандартный вывод Demucs: {process.stdout}"
                if process.stderr:
                    error_detail += f" Стандартный вывод ошибок Demucs: {process.stderr}"
                app.logger.error(error_detail)
                return jsonify({'error': 'Обработанный файл вокала не найден после выполнения Demucs.', 'details': error_detail}), 500

        except subprocess.CalledProcessError as e:
            app.logger.error(f"Error processing with Demucs (via docker run). Command: {' '.join(e.cmd)}. Return code: {e.returncode}")
            app.logger.error(f"Demucs stdout: {e.stdout}")
            app.logger.error(f"Demucs stderr: {e.stderr}")
            return jsonify({
                'error': 'Ошибка обработки песни с помощью Demucs',
                'command': ' '.join(e.cmd),
                'returncode': e.returncode,
                'stdout': e.stdout,
                'stderr': e.stderr
            }), 500
        except Exception as e:
            app.logger.error(f"An unexpected error occurred: {str(e)}")
            return jsonify({'error': f'Произошла непредвиденная ошибка: {str(e)}'}), 500
            
    else:
        return jsonify({'error': 'Недопустимый тип файла'}), 400

if __name__ == '__main__':
    # Создаем директории на хосте (через путь, видимый FlaskServise)
    # при старте Flask, если их нет.
    os.makedirs(FLASK_INPUT_DIR, exist_ok=True)
    os.makedirs(FLASK_OUTPUT_DIR_BASE, exist_ok=True)
    app.run(debug=True, host='0.0.0.0', port=5000) 