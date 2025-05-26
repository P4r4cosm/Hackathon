# Whisper Worker Service

Этот сервис представляет собой Python-воркер, который прослушивает очередь задач в RabbitMQ, выполняет распознавание речи на полученных аудиофайлах с помощью `openai-whisper` и отправляет результаты обратно.

## Основные функции

-   **Получение задач**: Воркер подключается к RabbitMQ и подписывается на очередь, указанную в переменной окружения `RABBITMQ_WHISPER_QUEUE_NAME`.
-   **Загрузка аудио**: При получении сообщения задачи, содержащего путь к аудиофайлу в MinIO (`input_minio_object_name`), воркер загружает этот файл.
-   **Распознавание речи**: Используется модель `openai-whisper` (название модели настраивается через `WHISPER_MODEL_NAME`, язык через `WHISPER_LANGUAGE`) для транскрибации аудио. Модели кэшируются в директории, указанной `WHISPER_CACHE_DIR` (внутри контейнера это `/app/.cache/whisper`, что монтируется в том `whisper_models_cache`).
-   **Сохранение результатов**: Результаты транскрибации (полный JSON и текстовый файл .txt) сохраняются обратно в MinIO в бакет `MINIO_BUCKET_NAME` с префиксом, указанным в сообщении задачи (`output_minio_prefix`, по умолчанию `whisper_output/`).
-   **Отправка уведомления о результате**: Информация о выполненной задаче (статус, пути к файлам результатов в MinIO, определенный язык) отправляется в RabbitMQ в обменник `RABBITMQ_RESULTS_EXCHANGE_NAME` с ключом маршрутизации `task.result.whisper` (или `task.result.whisper.error` в случае ошибки).

## Взаимодействие с другими сервисами

-   **RabbitMQ**: Для получения задач и отправки уведомлений о результатах.
-   **MinIO**: Для загрузки исходных аудиофайлов и сохранения результатов транскрибации.
-   **SoundService** (предположительно): Сервис, который ставит задачи в очередь для `whisper_worker` и получает уведомления о результатах.

## Переменные окружения

Основные переменные окружения для конфигурации воркера (большинство из них должны быть в вашем `.env` файле):

-   `LOG_LEVEL`: Уровень логирования (например, `INFO`, `DEBUG`, `ERROR`). По умолчанию `INFO`.

-   **RabbitMQ:**
    -   `RABBITMQ_HOST`: Хост RabbitMQ.
    -   `RABBITMQ_PORT`: Порт RabbitMQ.
    -   `RABBITMQ_USER`: Имя пользователя RabbitMQ.
    -   `RABBITMQ_PASS`: Пароль пользователя RabbitMQ.
    -   `RABBITMQ_VHOST`: Виртуальный хост RabbitMQ.
    -   `RABBITMQ_WHISPER_QUEUE_NAME`: Имя очереди для задач Whisper.
    -   `RABBITMQ_AUDIO_PROCESSING_EXCHANGE`: Имя обменника, откуда читаются задачи.
    -   `RABBITMQ_WHISPER_TASKS_ROUTING_KEY`: Ключ маршрутизации для задач Whisper.
    -   `RABBITMQ_RESULTS_EXCHANGE_NAME`: Имя обменника для публикации результатов.
    -   `RABBITMQ_TASK_RESULTS_ROUTING_KEY_PREFIX`: Префикс ключа маршрутизации для результатов (например, `task.result`).

-   **MinIO:**
    -   `MINIO_ENDPOINT`: Endpoint для MinIO (например, `minio:9000`).
    -   `MINIO_ACCESS_KEY`: Ключ доступа MinIO.
    -   `MINIO_SECRET_KEY`: Секретный ключ MinIO.
    -   `MINIO_BUCKET_NAME`: Имя бакета в MinIO для аудиофайлов.
    -   `MINIO_USE_SSL`: Использовать ли SSL для MinIO (`True`/`False`).

-   **Whisper:**
    -   `WHISPER_MODEL_NAME`: Имя модели Whisper для использования (например, `tiny`, `base`, `small`, `medium`, `large-v2`, `large-v3`). По умолчанию `base`.
    -   `WHISPER_LANGUAGE`: Язык для распознавания (например, `en`, `ru`). Если не указан, Whisper попытается определить язык автоматически.
    -   `WHISPER_CACHE_DIR`: Путь к директории для кэширования моделей Whisper внутри контейнера (монтируется в `whisper_models_cache`).

-   **Повторные попытки подключения:**
    -   `MAX_RETRIES_RABBITMQ`: Максимальное количество попыток подключения к RabbitMQ.
    -   `RECONNECT_DELAY_SECONDS`: Задержка между попытками подключения.

## Ожидаемый формат сообщения задачи (вход)

Сообщение в очереди RabbitMQ должно быть в формате JSON и содержать как минимум:

```json
{
  "task_id": "уникальный_id_задачи", // Этот ID будет использован как correlation_id
  "input_minio_object_name": "path/to/your/audiofile.mp3",
  "output_minio_prefix": "processed/whisper/" // Опционально, по умолчанию "whisper_output/"
  // Можно добавить другие параметры, например, конкретную модель или язык для этой задачи
  // "whisper_model_name": "large-v2", 
  // "whisper_language": "en"
}
```

*Примечание: `task_id` из тела сообщения будет проигнорирован, если `correlation_id` уже установлен у сообщения RabbitMQ. Рекомендуется использовать `correlation_id` для передачи `task_id`.*

## Формат сообщения о результате (выход)

Успешный результат:
```json
{
  "task_id": "уникальный_id_задачи",
  "status": "success",
  "service": "whisper",
  "original_input_object": "path/to/your/audiofile.mp3",
  "transcription_text_object": "processed/whisper/audiofile_transcription.txt",
  "transcription_json_object": "processed/whisper/audiofile_transcription.json",
  "detected_language": "ru"
}
```

Результат с ошибкой:
```json
{
  "task_id": "уникальный_id_задачи",
  "status": "error" или "critical_error",
  "service": "whisper",
  "original_input_object": "path/to/your/audiofile.mp3", // может отсутствовать при критической ошибке в самом начале
  "error_message": "Описание ошибки"
}
```

## Сборка и запуск

Сервис собирается и запускается через `docker-compose.yaml`.
Убедитесь, что все необходимые переменные окружения определены в файле `.env`.
Для использования GPU необходимо, чтобы Docker и docker-compose были настроены для работы с NVIDIA GPU, и в `docker-compose.yaml` для сервиса `whisper_worker` были указаны соответствующие `deploy` ресурсы.

## Кэширование моделей

Модели Whisper загружаются при первом использовании и кэшируются в Docker-том `whisper_models_cache` (монтируется в `/app/.cache/whisper` внутри контейнера). Это предотвращает повторную загрузку моделей при перезапуске воркера. 