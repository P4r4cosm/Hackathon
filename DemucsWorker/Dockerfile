# Используйте конкретный тег, например, с CUDA 12.1, который вам нужен
FROM pytorch/pytorch:2.1.0-cuda12.1-cudnn8-runtime
# Или FROM pytorch/pytorch:2.0.1-cuda11.8-cudnn8-runtime

WORKDIR /app

# Устанавливаем DEBIAN_FRONTEND в noninteractive, чтобы избежать диалогов при установке пакетов
ENV DEBIAN_FRONTEND=noninteractive

# Убедимся, что python3-pip есть и обновлен, и устанавливаем git (demucs может его требовать для некоторых моделей)
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    python3-pip \
    git \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Копируем requirements.txt и устанавливаем зависимости
COPY requirements.txt .
RUN python3 -m pip install --no-cache-dir --upgrade pip
RUN python3 -m pip install --no-cache-dir -r requirements.txt

# Копируем код воркера (предположим, он называется demucs_worker.py)
COPY app.py .

# Команда для запуска воркера
# Python использует буферизацию вывода по умолчанию.
# -u гарантирует, что stdout и stderr отправляются напрямую, что полезно для логов Docker.
CMD ["python3", "-u", "app.py"]