using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using SoundService.Data;
using SoundService.Models;

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
}