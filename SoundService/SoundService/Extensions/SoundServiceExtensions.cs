using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Minio;
using SoundService.Data;
using SoundService.Models;
using SoundService.Models.Settings;
using SoundService.Services;

namespace SoundService.Extensions;


public static class SoundServiceExtensions
{
    public static IServiceCollection AddApplicationDbContext(this IServiceCollection services, string connectionString)
    {
        return services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly("SoundService")));
    }

    public static IApplicationBuilder MigrateDatabase(this IApplicationBuilder app)
    {
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            
            try
            {
                logger.LogInformation("Начинаю миграцию базы данных...");
                db.Database.Migrate();
                logger.LogInformation("Миграция базы данных успешно завершена");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при выполнении миграции: {Message}", ex.Message);
                // В случае ошибки миграции пробуем создать БД напрямую
                try
                {
                    logger.LogInformation("Пытаюсь создать базу данных напрямую...");
                    db.Database.EnsureCreated();
                    logger.LogInformation("База данных успешно создана");
                }
                catch (Exception createEx)
                {
                    logger.LogError(createEx, "Ошибка при создании базы данных: {Message}", createEx.Message);
                }
            }
            
            return app;
        }
    }

    public static IServiceCollection AddElastic(this IServiceCollection services, Uri elasticUri)
    {
        var settings = new ElasticsearchClientSettings(elasticUri)
            .DefaultIndex("audio_records"); // optional

        var client = new ElasticsearchClient(settings);
        services.AddSingleton(client);
        return services;
    }

    public static IServiceCollection AddMinIO(this IServiceCollection services, MinioSettings minIoSettings)
    {
        services.AddSingleton<IMinioClient>(sp =>
        {
            var httpClient = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

            return new MinioClient()
                .WithEndpoint(minIoSettings.Endpoint, minIoSettings.Port)
                .WithCredentials(minIoSettings.AccessKey, minIoSettings.SecretKey)
                .Build();
        });

        services.AddSingleton<MinIOService>(sp =>
            new MinIOService(
                sp.GetRequiredService<IMinioClient>(),
                minIoSettings.BucketName));

        return services;
    }
}