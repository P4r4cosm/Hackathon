using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.EntityFrameworkCore;
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

    public async Task<AudioRecord> SaveAsync(AudioRecord audioRecord)
    {
        _dbContext.AudioRecords.Add(audioRecord); // Добавляем аудиозапись в контекст
        await _dbContext.SaveChangesAsync(); // Сохраняем изменения в базу
        return audioRecord;
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
    public async Task<AudioRecord?> GetTrackByIdAsync(int id)
    {
        return await _dbContext.AudioRecords.FirstOrDefaultAsync(ar => ar!.Id == id);
    }
    public async Task<List<AudioRecordForElastic>> GetAllAsync(int from = 0, int size = 20)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(q => q.MatchAll(new MatchAllQuery()))
        );
    
        if (!response.IsValidResponse)
        {
            _logger.LogError("Error getting documents from Elasticsearch: {ErrorReason}", response.DebugInformation);
            return new List<AudioRecordForElastic>();
        }
    
        return response.Documents.ToList();
    }
    
    public async Task<AudioRecordForElastic?> GetTrackTextByIdAsync(int id)
    {
        var response = await _elasticClient.GetAsync<AudioRecordForElastic>(id);
        if (!response.IsValidResponse)
        {
            _logger.LogError("Error getting document from Elasticsearch: {ErrorReason}", response.DebugInformation);
            return null;
        }
        
        if (!response.Found) 
        {
            _logger.LogWarning("Document with id {Id} not found in Elasticsearch", id);
            return null;
        }

        return response.Source;
    }
}

