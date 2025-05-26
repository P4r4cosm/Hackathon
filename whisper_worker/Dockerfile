ARG CUDA_VERSION=11.8.0
ARG UBUNTU_VERSION=22.04
ARG PYTHON_VERSION=3.9

FROM nvidia/cuda:${CUDA_VERSION}-cudnn8-devel-ubuntu${UBUNTU_VERSION}

ARG PYTHON_VERSION
ENV PYTHON_VERSION=${PYTHON_VERSION}
ENV DEBIAN_FRONTEND=noninteractive

# Установка Python и основных инструментов
RUN apt-get update && apt-get install -y --no-install-recommends \
    python${PYTHON_VERSION} \
    python${PYTHON_VERSION}-dev \
    python${PYTHON_VERSION}-distutils \
    python3-pip \
    ffmpeg \
    git \
    curl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Устанавливаем pip и обновляем его
RUN curl -sS https://bootstrap.pypa.io/get-pip.py | python${PYTHON_VERSION}
RUN python${PYTHON_VERSION} -m pip install --upgrade pip setuptools wheel

WORKDIR /app

COPY requirements.txt .

# Установка зависимостей Python
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py .

# Каталог для кэша моделей Whisper
ENV XDG_CACHE_HOME=/app/.cache
RUN mkdir -p ${XDG_CACHE_HOME}/whisper && chmod -R 777 ${XDG_CACHE_HOME}
ENV WHISPER_CACHE_DIR=${XDG_CACHE_HOME}/whisper

# Добавляем пользователя appuser для безопасности
RUN useradd -ms /bin/bash appuser
USER appuser

CMD ["python3", "app.py"] 