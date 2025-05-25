# Используем предварительно собранный образ historical-denoiser как базовый
# Убедитесь, что образ historical-denoiser:latest существует локально или в вашем репозитории Docker образов.
# Вы можете собрать его, перейдя в директорию 'historical-denoise' и выполнив: docker build -t historical-denoiser:latest .
FROM historical-denoiser:latest

# Установка Docker CLI (для Docker-out-of-Docker)
# Необходимо, чтобы Docker-сокет хоста (/var/run/docker.sock) был смонтирован в этот контейнер при запуске.
USER root 
RUN apt-get update && apt-get install -y --no-install-recommends \
    docker.io \
    && rm -rf /var/lib/apt/lists/*
# Примечание: в Miniconda образах пользователь по умолчанию часто root или jovyan.
# Если в базовом образе historical-denoiser пользователь не root, 
# может потребоваться добавить его в группу docker:
# RUN usermod -aG docker your_conda_user_name 
# USER your_conda_user_name # Вернуться к пользователю Conda, если он не root

# Установка дополнительных Python зависимостей для воркера (pika, minio)
# RUN conda run -n historical_denoiser pip install pika minio
# Или, если conda run не работает на этом этапе, можно попробовать так:
# SHELL ["conda", "run", "-n", "historical_denoiser", "/bin/bash", "-c"]
# RUN pip install pika minio
# Наиболее надежный способ - использовать /opt/conda/envs/historical_denoiser/bin/pip
RUN /opt/conda/envs/historical_denoiser/bin/pip install --no-cache-dir pika minio

# Устанавливаем рабочую директорию для воркера (может быть та же /app)
WORKDIR /app/worker_code

# Копируем код самого воркера
COPY app.py .

# Команда для запуска воркера
# Используем тот же ENTRYPOINT из базового образа historical-denoiser, который активирует conda окружение
# CMD будет python app.py
# ENTRYPOINT из базового образа: ["conda", "run", "-n", "historical_denoiser", "--no-capture-output"]
CMD ["python", "-u", "app.py"] 