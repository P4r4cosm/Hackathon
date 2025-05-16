FROM nginx:alpine

# Создаем простой index.html
RUN echo "<html><head><title>Frontend Service</title></head><body><h1>Frontend Service</h1><p>Заглушка для Frontend Service</p></body></html>" > /usr/share/nginx/html/index.html

# Открываем порты 80 и 443
EXPOSE 80
EXPOSE 443

CMD ["nginx", "-g", "daemon off;"] 