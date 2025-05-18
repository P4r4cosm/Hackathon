# Build stage
FROM node:18-alpine as build

WORKDIR /app

# Копируем файлы для установки зависимостей
COPY FrontendService/package*.json ./

# Устанавливаем зависимости
RUN npm install

# Копируем все файлы проекта
COPY FrontendService/ ./

# Собираем проект
RUN npm run build

# Production stage
FROM nginx:alpine

# Копируем собранный проект в директорию Nginx
COPY --from=build /app/dist /usr/share/nginx/html

# Копируем конфигурацию Nginx (при необходимости)
# COPY FrontendService/nginx.conf /etc/nginx/conf.d/default.conf

# Открываем порты 80 и 443
EXPOSE 80
EXPOSE 443

CMD ["nginx", "-g", "daemon off;"] 