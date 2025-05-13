FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AuthService/AuthService.csproj", "AuthService/"]
RUN dotnet restore "AuthService/AuthService.csproj"
COPY . .
WORKDIR "/src/AuthService"
RUN dotnet build "AuthService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AuthService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false


FROM base AS final
COPY --from=publish /app/publish .

# Создаем директорию для сертификатов внутри контейнера
RUN mkdir -p /app/https # Создаем директорию /app/https


# Копируем сертификат из контекста сборки Dockerfile в образ.
# Если .pfx файл лежит рядом с Dockerfile (внутри папки AuthService):
COPY ./cert.pfx /app/https/cert.pfx
RUN ls -l /app/https/
# Если .pfx файл лежит на уровень выше (рядом с docker-compose.yml), а Dockerfile в AuthService/:
# COPY ../aspnetcore-dev-cert.pfx /https/aspnetcore-dev-cert.pfx
# Выберите правильный путь в зависимости от расположения файла .pfx относительно Dockerfile

# Устанавливаем переменную окружения для Kestrel, чтобы он нашел сертификат
ENV KESTREL_CERTIFICATE_PATH=/app/https/cert.pfx

ENTRYPOINT ["dotnet", "AuthService.dll"]
#ENTRYPOINT ["tail", "-f", "/dev/null"]


