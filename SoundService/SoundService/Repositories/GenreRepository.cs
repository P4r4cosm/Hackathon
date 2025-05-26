using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.EntityFrameworkCore;
using SoundService.Data;
using SoundService.Models;

namespace SoundService.Repositories;

public class GenreRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<GenreRepository> _logger;
    private readonly AudioRecordRepository _audioRecordRepository;

    public GenreRepository(ApplicationDbContext dbContext, ElasticsearchClient elasticClient,
        ILogger<GenreRepository> logger,
        AudioRecordRepository audioRecordRepository)
    {
        _dbContext = dbContext;
        _elasticClient = elasticClient;
        _logger = logger;
        _audioRecordRepository = audioRecordRepository;
    }

    /// <summary>
    /// Редактирует название жанра по id (изменится у всех треков)
    /// изменяет его имя в postgres, а дальше у всех документов в elasticsearch
    /// </summary>
    /// <param name="id"></param>
    /// <param name="newName"></param>
    public async Task EditGenreName(int id, string newName)
    {
        var genre = await _dbContext.Genres.FirstOrDefaultAsync(a => a.Id == id);
        if (genre != null)
        {
            // 1. Обновляем записи в PostgreSQL
            var oldName =
                genre.Name; // Сохраняем старое имя на случай, если оно понадобится для логирования или сложной логики
            genre.Name = newName;
            if (oldName == newName)
            {
                _logger.LogInformation(
                    "Имя жанра ID {GenreId} не изменилось ('{NewName}'). Обновление Elasticsearch пропущено.",
                    id, newName);
                return; // Выходим, если имя не изменилось
            }

            await _dbContext.SaveChangesAsync(); // Сначала применяем изменения в БД

            // 2. Обновляем соответствующие записи в Elasticsearch
            // Продолжаем, только если имя действительно изменилось, чтобы избежать ненужных обновлений ES
            if (oldName != newName)
            {
                _logger.LogInformation(
                    "Имя жанра изменено с '{OldName}' на '{NewName}' для ID {GenreId}. Распространяется в Elasticsearch.",
                    oldName, newName, id);
                try
                {
                    var updateByQueryResponse =
                        await _elasticClient.UpdateByQueryAsync<AudioRecordForElastic>(u => u
                            .Query(q => q
                                .Nested(n => n
                                    .Path(p => p.Genres)
                                    .Query(nq => nq
                                        .Term(t => t
                                            .Field(f => f.Genres[0].Id)
                                            .Value(id.ToString())))))
                            .Script(s => s
                                .Source(@"
                                            for (int i = 0; i < ctx._source.genres.size(); i++) {
                                                if (ctx._source.genres[i].id == params.id) {
                                                    ctx._source.genres[i].name = params.newName;
                                                }
                                            }")
                                .Lang("painless")
                                .Params(p => p
                                    .Add("id", id)
                                    .Add("newName", newName))
                            )
                        );


                    if (!updateByQueryResponse.IsValidResponse)
                    {
                        var reason = "Причина неизвестна";
                        if (updateByQueryResponse.ElasticsearchServerError != null)
                        {
                            reason = updateByQueryResponse.ElasticsearchServerError.Error?.Reason ??
                                     updateByQueryResponse.ElasticsearchServerError.ToString();
                        }
                        else if (updateByQueryResponse.ApiCallDetails.OriginalException != null)
                        {
                            reason = updateByQueryResponse.ApiCallDetails.OriginalException.Message;
                        }

                        _logger.LogError("Не удалось обновить имя жанра в Elasticsearch для Genre ID {GenreId}. " +
                                         "Причина: {Reason}. DebugInfo: {DebugInfo}",
                            id,
                            reason,
                            updateByQueryResponse.DebugInformation);
                        // throw new Exception($"Ошибка обновления Elasticsearch: {reason}");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Запрос на обновление Elasticsearch для Genre ID {GenreId} с новым именем '{NewName}' выполнен. " +
                            "Обновлено документов: {UpdatedCount}, Неудач: {FailuresCount}, Общее количество документов затронутых запросом: {TotalCount}",
                            id, newName, updateByQueryResponse.Updated ?? 0, updateByQueryResponse.Failures?.Count ?? 0,
                            updateByQueryResponse.Total ?? 0);

                        if (updateByQueryResponse.Failures != null && updateByQueryResponse.Failures.Any())
                        {
                            foreach (var failure in updateByQueryResponse.Failures)
                            {
                                _logger.LogWarning(
                                    "Ошибка обновления Elasticsearch для документа ID {DocId} в индексе {Index}: {Cause} (Статус: {Status})",
                                    failure.Id, failure.Index, failure.Cause?.Reason, failure.Status);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Произошло исключение при обновлении Elasticsearch для Genre ID {GenreId}.",
                        id);
                    // Обработайте или перебросьте исключение
                }
            }
            else
            {
                _logger.LogWarning("Жанр с ID {GenreId} не найден для редактирования.", id);
            }
        }
    }
}