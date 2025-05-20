import os
import uuid
import subprocess
# import demucs # Не используется напрямую, кроме версии
from flask import Flask, request, send_from_directory, jsonify
from werkzeug.utils import secure_filename
import torch
import shutil # Для удаления папки

app = Flask(__name__)
print(f"PyTorch version: {torch.__version__}")
print(f"CUDA available: {torch.cuda.is_available()}") # Должно быть True
# import demucs # Если хотите печатать версию demucs
# print(f"Demucs version: {demucs.__version__}")


# Папки
UPLOAD_FOLDER = '/data/input'
OUTPUT_FOLDER = '/data/output'
ALLOWED_EXTENSIONS = {'wav', 'flac', 'mp3'} # Добавим mp3, т.к. ffmpeg его поддерживает

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

    # print("Demucs version:", demucs.__version__) # Раскомментируйте, если импортировали demucs
    print(f"CUDA available during request: {torch.cuda.is_available()}")

    if file and allowed_file(file.filename):
        original_filename_secure = secure_filename(file.filename)
        original_extension = original_filename_secure.rsplit('.', 1)[1].lower()

        track_id = str(uuid.uuid4()) # Это будет именем папки и basename файла для demucs

        # Входной файл будет сохранен с уникальным именем track_id и его оригинальным расширением
        input_filename_for_demucs = f"{track_id}.{original_extension}"
        input_path = os.path.join(UPLOAD_FOLDER, input_filename_for_demucs)

        # Выходная директория для этого трека (основная)
        track_output_base_dir = os.path.join(OUTPUT_FOLDER, track_id)

        model_name = "htdemucs" # Модель, используемая в demucs

        # Demucs создаст подпапку с именем модели и потом подпапку с именем входного файла (без расширения)
        # /data/output/{track_id}/{model_name}/{track_id}/vocals.wav
        final_vocals_dir = os.path.join(track_output_base_dir, model_name, track_id)
        vocals_path = os.path.join(final_vocals_dir, "vocals.wav")

        try:
            os.makedirs(UPLOAD_FOLDER, exist_ok=True) # Убедимся, что папка input есть
            os.makedirs(track_output_base_dir, exist_ok=True) # Создаем базовую выходную папку для трека
            file.save(input_path)

            cmd = [
                "python3", "-m", "demucs", # Рекомендуемый способ вызова demucs как модуля
                "-d", "cuda" if torch.cuda.is_available() else "cpu",
                "-n", model_name,
                "--two-stems", "vocals",
                "--shifts", "0", # Можно убрать для ускорения, если качество устраивает
                "--out", track_output_base_dir, # Указываем базовую выходную директорию
                input_path
            ]
            print(f"Executing command: {' '.join(cmd)}")

            result = subprocess.run(cmd, capture_output=True, text=True, check=False) # check=False для ручной проверки

            print(f"Demucs stdout:\n{result.stdout}")
            print(f"Demucs stderr:\n{result.stderr}")

            if result.returncode != 0:
                return jsonify({
                    'error': 'Ошибка при обработке файла Demucs',
                    'stderr': result.stderr,
                    'stdout': result.stdout,
                    'returncode': result.returncode
                }), 500

            if not os.path.exists(vocals_path):
                # Попытаемся найти файл, если структура немного другая (маловероятно, но для отладки)
                found_vocals = []
                for root, _, files in os.walk(track_output_base_dir):
                    for f_name in files:
                        if "vocals.wav" in f_name:
                            found_vocals.append(os.path.join(root, f_name))
                return jsonify({
                    'error': f'Не удалось найти обработанный файл vocals.wav по ожидаемому пути: {vocals_path}',
                    'expected_path': vocals_path,
                    'actual_content_of_output_dir': list(os.walk(track_output_base_dir)),
                    'found_vocals_if_any': found_vocals,
                    'demucs_stdout': result.stdout,
                    'demucs_stderr': result.stderr
                    }), 500

            # Отправляем файл
            response = send_from_directory(
                directory=final_vocals_dir, # Директория, где лежит vocals.wav
                path="vocals.wav",
                as_attachment=True,
                download_name=f"{track_id}_vocals.wav" # Имя файла для скачивания клиентом
            )

            # Очистка после отправки файла
            # Важно: это выполнится только ПОСЛЕ того, как файл будет полностью отправлен
            # Это делается с помощью response.call_on_close
            @response.call_on_close
            def cleanup_files():
                try:
                    print(f"Cleaning up input file: {input_path}")
                    os.remove(input_path)
                except OSError as e:
                    print(f"Error removing input file {input_path}: {e}")
                try:
                    print(f"Cleaning up output directory: {track_output_base_dir}")
                    shutil.rmtree(track_output_base_dir)
                except OSError as e:
                    print(f"Error removing output directory {track_output_base_dir}: {e}")

            return response

        except Exception as e:
            # Удаляем входной файл и выходную папку в случае любой ошибки
            if os.path.exists(input_path):
                try:
                    os.remove(input_path)
                except OSError as err_remove:
                    print(f"Error removing input file on exception: {err_remove}")
            if os.path.exists(track_output_base_dir):
                try:
                    shutil.rmtree(track_output_base_dir)
                except OSError as err_rmtree:
                    print(f"Error removing output directory on exception: {err_rmtree}")
            return jsonify({'error': f'Внутренняя ошибка сервера: {str(e)}', 'trace': traceback.format_exc()}), 500
    else:
        return jsonify({'error': 'Недопустимый тип файла или файл не предоставлен'}), 400

if __name__ == '__main__':
    import traceback # Для более детальных ошибок
    os.makedirs(UPLOAD_FOLDER, exist_ok=True)
    os.makedirs(OUTPUT_FOLDER, exist_ok=True)
    app.run(host='0.0.0.0', port=5000, debug=True) # debug=True для разработки