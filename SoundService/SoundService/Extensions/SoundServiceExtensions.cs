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
            options.UseNpgsql(connectionString));
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