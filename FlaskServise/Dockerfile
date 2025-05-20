# Используйте конкретный тег, например, с CUDA 11.8 или 12.1, который вам нужен
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

COPY requirements.txt .
# Обновляем pip и устанавливаем зависимости
RUN python3 -m pip install --no-cache-dir --upgrade pip
RUN python3 -m pip install --no-cache-dir -r requirements.txt

COPY app.py .

RUN mkdir -p /data/input /data/output
VOLUME /data

EXPOSE 5000

CMD ["gunicorn", "-b", "0.0.0.0:5000", "--timeout", "600", "--workers", "1", "app:app"]