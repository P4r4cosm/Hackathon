#!/bin/sh

# Выход при любой ошибке, чтобы сразу видеть проблему
set -e

echo "--- mc-init script started ---"
echo "DEBUG: MINIO_ROOT_USER=${MINIO_ROOT_USER}"
echo "DEBUG: MINIO_SERVICE_ACCESS_KEY=${MINIO_ACCESS_KEY}"
echo "DEBUG: MINIO_BUCKET_NAME=${MINIO_BUCKET_NAME}"
echo "------------------------------"

# Критически важная проверка: убедимся, что все нужные переменные установлены
if [ -z "${MINIO_ACCESS_KEY}" ] || [ -z "${MINIO_SECRET_KEY}" ] || [ -z "${MINIO_BUCKET_NAME}" ]; then
  echo "❌ КРИТИЧЕСКАЯ ОШИБКА: Одна или несколько переменных (MINIO_ACCESS_KEY, MINIO_SECRET_KEY, MINIO_BUCKET_NAME) не установлены!"
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
echo "✅ Бакет '${MINIO_BUCKET_NAME}' проверен/создан."

echo "Создание пользователя для сервисов: '${MINIO_ACCESS_KEY}'..."
# Используем || true, чтобы скрипт не падал, если пользователь уже существует при перезапуске
mc admin user add myminio "${MINIO_ACCESS_KEY}" "${MINIO_SECRET_KEY}" || true
echo "✅ Пользователь '${MINIO_ACCESS_KEY}' проверен/создан."

POLICY_NAME="service-policy-for-${MINIO_BUCKET_NAME}"
POLICY_FILE="/tmp/service_policy.json"

# ==============================================================================
# ▼▼▼ КЛЮЧЕВОЕ ИЗМЕНЕНИЕ ЗДЕСЬ ▼▼▼
# Создаем корректную политику с двумя раздельными блоками Statement:
# 1. Для действий на уровне бакета (проверить существование, получить список).
# 2. Для действий с объектами внутри бакета (скачать, загрузить, удалить).
# ==============================================================================
echo "Формирование корректной политики '${POLICY_NAME}'..."
cat <<EOF > "${POLICY_FILE}"
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:ListBucket",
        "s3:GetBucketLocation"
      ],
      "Resource": [
        "arn:aws:s3:::${MINIO_BUCKET_NAME}"
      ]
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject"
      ],
      "Resource": [
        "arn:aws:s3:::${MINIO_BUCKET_NAME}/*"
      ]
    }
  ]
}
EOF

# Удаляем старую политику (если есть) и создаем новую.
# Это гарантирует, что политика всегда актуальна и идемпотентна.
echo "Создание/обновление политики '${POLICY_NAME}' в MinIO..."
mc admin policy rm myminio "${POLICY_NAME}" 2>/dev/null || true
mc admin policy create myminio "${POLICY_NAME}" "${POLICY_FILE}"
echo "✅ Политика '${POLICY_NAME}' создана/обновлена."

echo "Назначение политики '${POLICY_NAME}' пользователю '${MINIO_ACCESS_KEY}'..."
mc admin policy attach myminio "${POLICY_NAME}" --user "${MINIO_ACCESS_KEY}"
echo "✅ Политика назначена."

# Этот блок оставлен без изменений
if [ -d "/init-audio/" ] && [ -n "$(ls -A /init-audio/)" ]; then
  echo "🔄 Обнаружены файлы в /init-audio/. Зеркалирую содержимое в myminio/${MINIO_BUCKET_NAME}/..."
  mc mirror --overwrite /init-audio/ myminio/"${MINIO_BUCKET_NAME}"/
  echo "Содержимое бакета myminio/${MINIO_BUCKET_NAME}/ после зеркалирования:"
  mc ls --recursive myminio/"${MINIO_BUCKET_NAME}"/
else
  echo "ℹ️  Папка /init-audio/ пуста или не существует, зеркалирование пропущено."
fi

echo "✅ Процесс инициализации MinIO успешно завершен."