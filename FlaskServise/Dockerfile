# Используем официальный образ Python
FROM python:3.9-slim

# Устанавливаем рабочую директорию в контейнере
WORKDIR /app

# Устанавливаем Docker CLI, чтобы FlaskServise мог вызывать docker exec
# Это потребует доступа к Docker-сокету хоста при запуске через docker-compose
RUN apt-get update && apt-get install -y --no-install-recommends docker.io && rm -rf /var/lib/apt/lists/*

# Копируем файл с зависимостями
COPY FlaskServise/requirements.txt /app/requirements.txt

# Устанавливаем зависимости
RUN pip install --no-cache-dir -r /app/requirements.txt

# Копируем код приложения в рабочую директорию
COPY FlaskServise/app.py /app/app.py

# Указываем порт, на котором будет работать приложение
EXPOSE 5000

# Команда для запуска приложения
CMD ["python", "app.py"] 