#!/bin/sh
set -e # Выходить немедленно, если команда завершается с ненулевым статусом

ES_HOST="elasticsearch" # Имя сервиса Elasticsearch в docker-compose
ES_PORT="9200"
INDEX_NAME="audio_records"
MAPPING_FILE_PATH="/usr/share/elasticsearch/config/audio_records_mapping.json" # Путь внутри контейнера elasticsearch-setup

# Ожидание доступности Elasticsearch
echo "Waiting for Elasticsearch to be ready..."
until curl -s "http://${ES_HOST}:${ES_PORT}" -o /dev/null; do
  sleep 1
done
echo "Elasticsearch is up."

# Проверка, существует ли индекс
STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://${ES_HOST}:${ES_PORT}/${INDEX_NAME}")

if [ "$STATUS_CODE" -eq 200 ]; then
  echo "Index '${INDEX_NAME}' already exists."
elif [ "$STATUS_CODE" -eq 404 ]; then
  echo "Index '${INDEX_NAME}' does not exist. Creating index with mapping..."
  # Создание индекса с маппингом
  curl -X PUT "http://${ES_HOST}:${ES_PORT}/${INDEX_NAME}" \
       -H 'Content-Type: application/json' \
       -d "@${MAPPING_FILE_PATH}" # Используем @ для передачи содержимого файла
  echo "" # Для новой строки после вывода curl
  echo "Index '${INDEX_NAME}' created."
else
  echo "Error checking or creating index '${INDEX_NAME}'. HTTP Status: ${STATUS_CODE}"
  # Показать вывод, если есть
  curl -v "http://${ES_HOST}:${ES_PORT}/${INDEX_NAME}"
  exit 1
fi

# (Опционально) Создание/обновление шаблона индекса - очень хорошая практика
TEMPLATE_NAME="audio_records_template"
TEMPLATE_FILE_PATH="/usr/share/elasticsearch/config/audio_records_template.json" # Предположим, у вас есть такой файл

# Пример файла audio_records_template.json:
# {
#   "index_patterns": ["audio_records*"],
#   "template": {
#     "mappings": { ... (ваш маппинг из audio_records_mapping.json) ... },
#     "settings": { "number_of_shards": 1 }
#   }
# }
# Если у вас есть такой файл, раскомментируйте следующие строки:
#
# echo "Creating/Updating index template '${TEMPLATE_NAME}'..."
# curl -X PUT "http://${ES_HOST}:${ES_PORT}/_index_template/${TEMPLATE_NAME}" \
#      -H 'Content-Type: application/json' \
#      -d "@${TEMPLATE_FILE_PATH}"
# echo ""
# echo "Index template '${TEMPLATE_NAME}' processed."


echo "Elasticsearch setup finished."
exit 0