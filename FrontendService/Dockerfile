FROM node:18-alpine as build

WORKDIR /app

COPY FrontendService/package*.json ./

RUN npm install

COPY FrontendService/ ./

RUN npm run build

FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html

# Открываем порты 80 и 443
EXPOSE 80
EXPOSE 443

CMD ["nginx", "-g", "daemon off;"] 