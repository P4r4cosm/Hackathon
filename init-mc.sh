#!/bin/sh

# Выход при любой ошибке, чтобы сразу видеть проблему
set -e

echo "--- mc-init script started ---"
echo "DEBUG: MINIO_ROOT_USER=${MINIO_ROOT_USER}"
# echo "DEBUG: MINIO_ROOT_PASSWORD=${MINIO_ROOT_PASSWORD}" # Осторожно с выводом паролей в лог
echo "DEBUG: MINIO_SERVICE_ACCESS_KEY=${MINIO_ACCESS_KEY}"
# echo "DEBUG: MINIO_SERVICE_SECRET_KEY=${MINIO_SERVICE_SECRET_KEY}" # Осторожно с выводом паролей в лог
echo "DEBUG: MINIO_BUCKET_NAME=${MINIO_BUCKET_NAME}"
echo "------------------------------"

# Критически важная проверка: убедимся, что переменные для сервисного ключа НЕ ПУСТЫЕ
if [ -z "${MINIO_ACCESS_KEY}" ]; then
  echo "❌ КРИТИЧЕСКАЯ ОШИБКА: Переменная MINIO_ACCESS_KEY не установлена или пуста!"
  exit 1
fi
if [ -z "${MINIO_SECRET_KEY}" ]; then
  echo "❌ КРИТИЧЕСКАЯ ОШИБКА: Переменная MINIO_SECRET_KEY не установлена или пуста!"
  exit 1
fi
if [ -z "${MINIO_BUCKET_NAME}" ]; then
  echo "❌ КРИТИЧЕСКАЯ ОШИБКА: Переменная MINIO_BUCKET_NAME не установлена или пуста!"
  exit 1
fi

echo "Ожидание доступности MinIO и настройка mc alias 'myminio'..."
# Цикл ожидания, пока MinIO не станет доступен
COUNT=0
MAX_RETRIES=12 # ~60 секунд
until mc alias set myminio http://minio:9000 "${MINIO_ROOT_USER}" "${MINIO_ROOT_PASSWORD}" --api S3v4; do
  COUNT=$((COUNT + 1))
  if [ ${COUNT} -ge ${MAX_RETRIES} ]; then
    echo "❌ MinIO не стал доступен после ${MAX_RETRIES} попыток. Проверьте контейнер minio."
    exit 1
  fi
  echo "MinIO недоступен (попытка ${COUNT}/${MAX_RETRIES}), ожидание 5 секунд..."
  sleep 5
done
echo "✅ MinIO доступен и mc alias 'myminio' для root пользователя настроен."

echo "Создание бакета myminio/${MINIO_BUCKET_NAME} (если не существует)..."
mc mb --ignore-existing myminio/"${MINIO_BUCKET_NAME}"

echo "Создание пользователя для веб-сервиса: '${MINIO_ACCESS_KEY}'"
# Используем кавычки вокруг переменных, чтобы видеть, если они пустые
mc admin user add myminio "${MINIO_ACCESS_KEY}" "${MINIO_SECRET_KEY}" || \
  echo "ℹ️ Пользователь '${MINIO_ACCESS_KEY}' уже существует или произошла ошибка при его создании (это может быть нормально при перезапуске)."

POLICY_NAME="service-policy-${MINIO_BUCKET_NAME}"
POLICY_FILE="/tmp/service_policy.json"

echo "Создание/обновление политики '${POLICY_NAME}' для бакета '${MINIO_BUCKET_NAME}'..."
cat <<EOF > "${POLICY_FILE}"
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::${MINIO_BUCKET_NAME}/*",
        "arn:aws:s3:::${MINIO_BUCKET_NAME}"
      ]
    }
  ]
}
EOF

# Удаляем старую политику (если есть) и создаем новую.
# Это гарантирует, что политика всегда актуальна.
mc admin policy rm myminio "${POLICY_NAME}" 2>/dev/null || true
mc admin policy create myminio "${POLICY_NAME}" "${POLICY_FILE}"
echo "Политика '${POLICY_NAME}' создана/обновлена."

echo "Назначение политики '${POLICY_NAME}' пользователю '${MINIO_ACCESS_KEY}'..."
mc admin policy attach myminio "${POLICY_NAME}" --user "${MINIO_ACCESS_KEY}" || \
  echo "⚠️  Не удалось назначить политику '${POLICY_NAME}' пользователю '${MINIO_ACCESS_KEY}' (возможно, уже назначена или другая ошибка)."

echo "🔄 Зеркалирую все содержимое /init-audio/ в myminio/${MINIO_BUCKET_NAME}/ сохраняя структуру..."
mc mirror --overwrite /init-audio/ myminio/"${MINIO_BUCKET_NAME}"/

echo "Содержимое бакета myminio/${MINIO_BUCKET_NAME}/ после зеркалирования:"
mc ls --recursive myminio/"${MINIO_BUCKET_NAME}"/

echo "✅ Процесс инициализации успешно завершен."