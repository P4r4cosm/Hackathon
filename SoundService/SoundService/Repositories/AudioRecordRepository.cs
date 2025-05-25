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

    public AudioRecordRepository(ApplicationDbContext dbContext, ElasticsearchClient elasticClient,
        ILogger<AudioRecordRepository> logger)
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
                .Index(audioRecord) // Имя индекса(audioRecord)
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

    public async Task<List<Author>> GetAllAuthorsAsync()
    {
        return await _dbContext.Authors.ToListAsync();
    }

    public async Task<List<Genre>> GetAllGenresAsync()
    {
        return await _dbContext.Genres.ToListAsync();
    }

    public async Task<List<ThematicTag>> GetAllThematicTagsAsync()
    {
        return await _dbContext.ThematicTags.ToListAsync();
    }

    public async Task<List<Keyword>> GetAllKeywordsAsync()
    {
        return await _dbContext.Keywords.ToListAsync();
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

    public async Task<List<AudioRecordForElastic?>> GetTracksByAuthor(int id, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(q => q.Term(t => t.Field(f => f.AuthorId).Value(id)))
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Error getting documents from Elasticsearch: {ErrorReason}", response.DebugInformation);
            return new List<AudioRecordForElastic?>();
        }

        return response.Documents.ToList();
    }

    public async Task<List<AudioRecordForElastic?>> GetTracksByYear(int year, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(q => q.Term(t => t.Field(f => f.Year.ToString()).Value(year.ToString())))
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Error getting documents from Elasticsearch: {ErrorReason}", response.DebugInformation);
            return new List<AudioRecordForElastic?>();
        }

        return response.Documents.ToList();
    }

    public async Task<List<AudioRecordForElastic>> GetTracksByGenre(int genreId, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(nq => nq // Запрос внутри nested-объекта (nq - это QueryContainer)
                .Term(t => t // Конфигуратор для TermQuery
                    .Field(f => f.Genres[0].Name) // Указываем поле Id внутри объекта GenreDocument
                    // f.Genres[0] - это просто для доступа к типу GenreDocument,
                    // Elasticsearch поймет, что искать нужно по всем элементам массива.
                    .Value("Synthpunk")
                )
            )
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Error fetching tracks by genre from Elasticsearch: {DebugInfo}",
                response.DebugInformation);
            return new List<AudioRecordForElastic>();
        }

        return response.Documents.ToList();
    }
}