import os
import subprocess
import uuid # Для уникальных имен временных контейнеров
import time # Для небольшой задержки
from flask import Flask, request, send_from_directory, jsonify
from werkzeug.utils import secure_filename
# import shutil # shutil не используется, можно удалить, если ранее добавлялся для других целей

app = Flask(__name__)

ALLOWED_EXTENSIONS = {'wav', 'flac'}

# --- Конфигурация Docker и путей ---
DEMUCS_IMAGE_NAME = "local/demucs_app" # Docker-образ Demucs (должен быть собран)
DEMUCS_MODELS_VOLUME = "demucs_models_cache" # Именованный том для кэша моделей Demucs

# Динамическое определение пути к общей директории shared_docker_data
# shared_docker_data должна находиться в директории Hackathon,
# а Hackathon на один уровень выше директории, где лежит app.py (FlaskServise)
script_dir = os.path.dirname(os.path.abspath(__file__))      # Директория FlaskServise
hackathon_root_dir = os.path.abspath(os.path.join(script_dir, '..')) # Поднимаемся на один уровень до Hackathon
HOST_SHARED_DIR_PATH = os.path.join(hackathon_root_dir, "shared_docker_data")

# Пути для Flask (относительно HOST_SHARED_DIR_PATH)
FLASK_INPUT_DIR = os.path.join(HOST_SHARED_DIR_PATH, "input_files")
FLASK_OUTPUT_DIR_BASE = os.path.join(HOST_SHARED_DIR_PATH, "output_files")

# Пути внутри контейнера Demucs (куда монтируется HOST_SHARED_DIR_PATH)
DEMUCS_CONTAINER_MOUNT_POINT = "/data_mount" 
DEMUCS_CMD_INPUT_DIR = os.path.join(DEMUCS_CONTAINER_MOUNT_POINT, "input_files")
DEMUCS_CMD_OUTPUT_DIR = os.path.join(DEMUCS_CONTAINER_MOUNT_POINT, "output_files")
# --- Конец конфигурации --- 

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
    
    original_filename_for_cleanup = None
    track_name_for_cleanup = None
    model_name_for_cleanup = "htdemucs_ft" # Используемая модель Demucs

    if file and allowed_file(file.filename):
        original_filename_for_cleanup = secure_filename(file.filename)
        track_name_for_cleanup = os.path.splitext(original_filename_for_cleanup)[0]

        os.makedirs(FLASK_INPUT_DIR, exist_ok=True)
        os.makedirs(FLASK_OUTPUT_DIR_BASE, exist_ok=True)
        
        temp_input_path_flask = os.path.join(FLASK_INPUT_DIR, original_filename_for_cleanup)
        file.save(temp_input_path_flask)
        
        time.sleep(0.5) # Небольшая задержка для полной записи файла

        demucs_input_file_in_container = os.path.join(DEMUCS_CMD_INPUT_DIR, original_filename_for_cleanup)

        demucs_command_str = (
            f"python3 -m demucs -n {model_name_for_cleanup} "
            f"--two-stems vocals --shifts 1 --overlap 0.3 "
            f"--out {DEMUCS_CMD_OUTPUT_DIR} {demucs_input_file_in_container}"
        )
        
        temp_container_name = f"demucs_worker_{uuid.uuid4().hex[:8]}"

        docker_run_command = [
            "docker", "run", "--rm",
            "--name", temp_container_name,
            "-v", f"{HOST_SHARED_DIR_PATH}:{DEMUCS_CONTAINER_MOUNT_POINT}", 
            "-v", f"{DEMUCS_MODELS_VOLUME}:/data/models", # Кэширование моделей Demucs
            DEMUCS_IMAGE_NAME,
            "sh", "-c", demucs_command_str
        ]
        
        try:
            app.logger.info(f"Executing Demucs: {' '.join(docker_run_command)}")
            process = subprocess.run(docker_run_command, check=True, capture_output=True, text=True)
            app.logger.info(f"Demucs stdout: {process.stdout}")
            if process.stderr:
                 app.logger.warning(f"Demucs stderr: {process.stderr}") # stderr не всегда означает ошибку
            
            expected_vocals_path = os.path.join(
                FLASK_OUTPUT_DIR_BASE, 
                model_name_for_cleanup, 
                track_name_for_cleanup, 
                "vocals.wav"
            )

            if os.path.exists(expected_vocals_path):
                return send_from_directory(
                    directory=os.path.dirname(expected_vocals_path), 
                    path="vocals.wav",
                    as_attachment=True
                )
            else:
                error_detail = f"Обработанный файл vocals.wav не найден: {expected_vocals_path}."
                if process.stdout: error_detail += f" Demucs stdout: {process.stdout}"
                if process.stderr: error_detail += f" Demucs stderr: {process.stderr}"
                app.logger.error(error_detail)
                return jsonify({'error': 'Обработанный файл вокала не найден после выполнения Demucs.', 'details': error_detail}), 500

        except subprocess.CalledProcessError as e:
            app.logger.error(f"Ошибка обработки Demucs. Команда: {' '.join(e.cmd)}. Код: {e.returncode}")
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
            app.logger.error(f"Непредвиденная ошибка: {str(e)}")
            return jsonify({'error': f'Произошла непредвиденная ошибка: {str(e)}'}), 500
        finally:
            # Очистка файлов
            if original_filename_for_cleanup:
                input_file_to_delete = os.path.join(FLASK_INPUT_DIR, original_filename_for_cleanup)
                if os.path.exists(input_file_to_delete):
                    try:
                        os.remove(input_file_to_delete)
                        app.logger.info(f"Удален входной файл: {input_file_to_delete}")
                    except OSError as e_remove_input:
                        app.logger.error(f"Ошибка удаления входного файла {input_file_to_delete}: {e_remove_input}")
                
                if track_name_for_cleanup: 
                    no_vocals_file_to_delete = os.path.join(
                        FLASK_OUTPUT_DIR_BASE,
                        model_name_for_cleanup,
                        track_name_for_cleanup,
                        "no_vocals.wav"
                    )
                    if os.path.exists(no_vocals_file_to_delete):
                        try:
                            os.remove(no_vocals_file_to_delete)
                            app.logger.info(f"Удален файл no_vocals.wav: {no_vocals_file_to_delete}")
                        except OSError as e_remove_novocals:
                            app.logger.error(f"Ошибка удаления no_vocals.wav {no_vocals_file_to_delete}: {e_remove_novocals}")
            
    else:
        return jsonify({'error': 'Недопустимый тип файла'}), 400

if __name__ == '__main__':
    os.makedirs(FLASK_INPUT_DIR, exist_ok=True)
    os.makedirs(FLASK_OUTPUT_DIR_BASE, exist_ok=True)
    app.logger.info(f"Общая директория на хосте (вычислено): {HOST_SHARED_DIR_PATH}")
    app.logger.info(f"Директория для входных файлов Flask: {FLASK_INPUT_DIR}")
    app.logger.info(f"Базовая директория для выходных файлов Flask: {FLASK_OUTPUT_DIR_BASE}")
    
    app.run(debug=True, host='0.0.0.0', port=5000) 