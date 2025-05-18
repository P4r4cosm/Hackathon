import os
import subprocess
from flask import Flask, request, send_from_directory, jsonify
from werkzeug.utils import secure_filename

app = Flask(__name__)

ALLOWED_EXTENSIONS = {'wav', 'flac'}

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

        # Определяем пути относительно текущего файла app.py
        service_dir = os.path.dirname(os.path.abspath(__file__))
        demucs_project_root = os.path.abspath(os.path.join(service_dir, '..', 'demucs'))
        
        actual_demucs_input_dir = os.path.join(demucs_project_root, 'input')
        actual_demucs_output_base_dir = os.path.join(demucs_project_root, 'output', 'htdemucs_ft')

        # Создаем папку для входных файлов Demucs, если ее нет
        os.makedirs(actual_demucs_input_dir, exist_ok=True)
        
        temp_input_path = os.path.join(actual_demucs_input_dir, filename)
        file.save(temp_input_path)

        track_name_without_ext = os.path.splitext(filename)[0]
        
        # Аргумент track для команды make - только имя файла
        demucs_track_arg = filename
        
        # Параметры для Demucs
        extra_args = "splittrack=vocals shifts=2 overlap=0.3"
        
        # Формируем команду для Demucs
        command = f'make run track={demucs_track_arg} model=htdemucs_ft {extra_args}'
        
        # Рабочая директория для make - корень проекта Demucs
        cwd_for_make = demucs_project_root
        
        try:
            process = subprocess.run(command, shell=True, check=True, capture_output=True, text=True, cwd=cwd_for_make)
            
            # Ожидаемый путь к папке с результатом для данного трека
            expected_demucs_track_output_dir = os.path.join(actual_demucs_output_base_dir, track_name_without_ext)
            # Ожидаемый путь к файлу с вокалом
            processed_vocals_path = os.path.join(expected_demucs_track_output_dir, "vocals.wav") # Demucs обычно сохраняет в .wav

            if os.path.exists(processed_vocals_path):
                return send_from_directory(directory=expected_demucs_track_output_dir, path="vocals.wav", as_attachment=True)
            else:
                # Формируем более детальное сообщение об ошибке, если файл не найден
                error_detail = f"Обработанный файл с вокалом не найден по пути {processed_vocals_path}."
                if process.stdout:
                    error_detail += f" Стандартный вывод Demucs: {process.stdout}"
                if process.stderr:
                    error_detail += f" Стандартный вывод ошибок Demucs: {process.stderr}"
                return jsonify({'error': 'Обработанный файл вокала не найден после выполнения Demucs.', 'details': error_detail}), 500

        except subprocess.CalledProcessError as e:
            if os.path.exists(temp_input_path):
                os.remove(temp_input_path)
            return jsonify({
                'error': 'Ошибка обработки песни с помощью Demucs',
                'command': command,
                'returncode': e.returncode,
                'stdout': e.stdout,
                'stderr': e.stderr
            }), 500
        except Exception as e:
            if os.path.exists(temp_input_path):
                os.remove(temp_input_path)
            return jsonify({'error': str(e)}), 500
        finally:
            # Очистка: удаляем временный входной файл, если он все еще существует
            if os.path.exists(temp_input_path):
                 os.remove(temp_input_path)
    else:
        return jsonify({'error': 'Недопустимый тип файла'}), 400

if __name__ == '__main__':
    app.run(debug=True, host='0.0.0.0', port=5000) 