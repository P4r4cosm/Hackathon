# Используйте базовый образ с Python и CUDA, если планируется GPU.
# Выберите версию CUDA, совместимую с вашим драйвером NVIDIA и PyTorch.
# Например, для CUDA 11.8:
# FROM nvidia/cuda:11.8.0-cudnn8-runtime-ubuntu22.04
# Или для CUDA 12.1:
FROM nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04
# Если вы НЕ планируете использовать GPU (или хотите отладить на CPU), можно использовать:
# FROM python:3.10-slim

# Устанавливаем некоторые базовые утилиты и Python
# Для nvidia/cuda образов python часто уже есть, но pip может понадобиться
# Для python:3.10-slim, apt-get update и python3-pip не нужны, так как python и pip уже есть.

# Устанавливаем переменные окружения для non-interactive установки
ENV DEBIAN_FRONTEND=noninteractive
ENV PYTHONUNBUFFERED=1 \
    # Путь для кэша pip
    PIP_CACHE_DIR=/var/cache/pip \
    # Путь для кэша моделей Hugging Face Transformers (используется faster-whisper)
    HF_HOME=/app/.cache/huggingface \
    # Путь для кэша моделей faster-whisper (если он отличается от HF_HOME или для явности)
    XDG_CACHE_HOME=/app/.cache

# Создаем директорию для кэша и приложения
RUN mkdir -p /app/.cache/huggingface /app/.cache/whisper && \
    chown -R 1000:1000 /app/.cache && \
    chmod -R 775 /app/.cache

# Для nvidia/cuda образов:
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    python3-pip \
    python3-dev \
    ffmpeg \
    git \
    # Очистка
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Если используется python:3.10-slim, установка ffmpeg и git будет такой же,
# но python3-pip и python3-dev не нужны.

# Устанавливаем рабочую директорию
WORKDIR /app

# Копируем файл зависимостей
COPY requirements.txt .

# Устанавливаем зависимости Python
# --no-cache-dir чтобы не занимать место кэшем pip внутри образа
# Если вы указали конкретную версию torch в requirements.txt, она будет установлена.
# В противном случае, faster-whisper потянет совместимую версию.
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

# Копируем код приложения
COPY worker.py .

# Создаем пользователя без привилегий для запуска приложения (хорошая практика)
# RUN useradd -ms /bin/bash appuser
# USER appuser
# Если используете USER appuser, убедитесь, что у него есть права на /app/.cache

# Команда для запуска воркера
CMD ["python3", "worker.py"]