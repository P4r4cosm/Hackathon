using Elastic.Clients.Elasticsearch;
using SoundService.Data;
using SoundService.Models;

namespace SoundService.Repositories;

public class AudioRecordRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<AudioRecordRepository> _logger;

    public AudioRecordRepository(ApplicationDbContext dbContext, ElasticsearchClient elasticClient, ILogger<AudioRecordRepository> logger)
    {
        _dbContext = dbContext;
        _elasticClient = elasticClient;
        _logger = logger;
    }

    public async Task SaveAsync(AudioRecord audioRecord)
    {
        _dbContext.AudioRecords.Add(audioRecord); // Добавляем аудиозапись в контекст
        await _dbContext.SaveChangesAsync(); // Сохраняем изменения в базу
    }

    public async Task SaveAsync(AudioRecordForElastic audioRecord)
    {
        var bulkResponse = await _elasticClient.BulkAsync(b => b
            .Index(audioRecord)// Имя индекса(audioRecord)
        );
        if (!bulkResponse.IsValidResponse)
        {
            _logger.LogError("Error saving to Elasticsearch: {ErrorReason}", bulkResponse.DebugInformation);
        }
        else
        {
            _logger.LogInformation("Elasticsearch save complete.");
        }
    }
}

