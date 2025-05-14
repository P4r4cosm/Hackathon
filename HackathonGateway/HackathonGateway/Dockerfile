FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HackathonGateway/HackathonGateway/HackathonGateway.csproj", "HackathonGateway/"]
RUN dotnet restore "HackathonGateway/HackathonGateway.csproj"
COPY . .
WORKDIR "/src/HackathonGateway/HackathonGateway"
RUN dotnet build "./HackathonGateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HackathonGateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
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

ENTRYPOINT ["dotnet", "HackathonGateway.dll"]
#ENTRYPOINT ["tail", "-f", "/dev/null"]