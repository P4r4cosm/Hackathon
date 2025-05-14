FROM nginx:alpine

# Удаляем существующую конфигурацию
RUN rm /etc/nginx/conf.d/default.conf

# Копируем конфигурационный файл для nginx
COPY AuthServiceFrontend/nginx.conf /etc/nginx/conf.d/default.conf

# Копируем файлы приложения в каталог nginx
COPY AuthServiceFrontend/index.html /usr/share/nginx/html/
COPY AuthServiceFrontend/style.css /usr/share/nginx/html/
COPY AuthServiceFrontend/script.js /usr/share/nginx/html/

# Открываем только порт 80
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"] 