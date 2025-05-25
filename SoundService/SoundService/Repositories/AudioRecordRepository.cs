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

    public async Task EditTitleAsync(int id, string title)
    {
        var track = await _dbContext.AudioRecords.FirstOrDefaultAsync(a => a.Id == id);
        if (track != null)
        {
            var oldName =
                track.Title; // Сохраняем старое имя на случай, если оно понадобится для логирования или сложной логики
            track.Title = title;
            if (oldName == title)
            {
                _logger.LogInformation(
                    "Название трека ID {TrackId} не изменилось ('{Title}'). Обновление Elasticsearch пропущено.",
                    id, title);
                return; // Выходим, если имя не изменилось
            }

            await _dbContext.SaveChangesAsync();
            // Получаем аудиозапись из Elasticsearch (для проверки существования)
            var elasticRecord = await GetTrackTextByIdAsync(id);
            if (elasticRecord == null)
            {
                _logger.LogWarning(
                    "Документ с ID {Id} не найден в Elasticsearch. Продолжаем обновление только в базе данных.", id);
            }

            // Обновляем поле Title в Elasticsearch
            elasticRecord.Title = title;

            // Сохраняем изменения обратно в Elasticsearch
            await SaveAsync(elasticRecord);

            _logger.LogInformation(
                "Название аудиозаписи с ID {Id} успешно обновлено в базе данных и Elasticsearch на '{Title}'.", id,
                title);
        }
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

    public async Task SaveAsync(IEnumerable<AudioRecordForElastic> audioRecords)
    {
        var bulkResponse = await _elasticClient.BulkAsync(b => b
            .IndexMany(audioRecords)
        );
        if (!bulkResponse.IsValidResponse)
        {
            _logger.LogError("Error saving multiple documents to Elasticsearch: {ErrorReason}",
                bulkResponse.DebugInformation);
        }
        else
        {
            _logger.LogInformation("Elasticsearch bulk save complete. Saved {count} documents", audioRecords.Count());
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

    public async Task<List<AudioRecordForElastic>> GetTracksByGenreId(int genreId, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records")
            .From(from)
            .Size(size)
            .Query(q => q
                .Nested(n => n
                    .Path(p => p.Genres) // Указываем путь до nested-объектов
                    .Query(nq => nq
                        .Term(t => t
                                .Field(f => f.Genres[0].Id) // Поле Id внутри Genre
                                .Value(genreId.ToString()) // TermQuery требует строку
                        )
                    )
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

    public async Task<List<AudioRecordForElastic>> GetTracksByKeywords(IEnumerable<string> keywords, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records") // Указываем индекс
            .From(from) // Для пагинации
            .Size(size) // Для пагинации
            .Query(q => q
                .Terms(t => t // Используем Terms query
                        .Field(f => f.Keywords) // Указываем поле для поиска

                        //.Terms(new TermsQueryField(keywords.Cast<object>().ToList())) // Для NEST 7.x
                        .Terms(new TermsQueryField(keywords.Select(name => FieldValue.String(name))
                            .ToArray())) // Для Elastic.Clients.Elasticsearch 8.x
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

    public async Task<List<AudioRecordForElastic>> GetTracksByThematicTags(IEnumerable<string> tags, int from, int size)
    {
        var response = await _elasticClient.SearchAsync<AudioRecordForElastic>(s => s
            .Index("audio_records") // Указываем индекс
            .From(from) // Для пагинации
            .Size(size) // Для пагинации
            .Query(q => q
                .Terms(t => t // Используем Terms query
                        .Field(f => f.ThematicTags) // Указываем поле для поиска

                        //.Terms(new TermsQueryField(keywords.Cast<object>().ToList())) // Для NEST 7.x
                        .Terms(new TermsQueryField(tags.Select(name => FieldValue.String(name))
                            .ToArray())) // Для Elastic.Clients.Elasticsearch 8.x
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