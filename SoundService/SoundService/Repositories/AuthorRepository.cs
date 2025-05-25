using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using SoundService.Data;
using SoundService.Models;

namespace SoundService.Repositories;

public class AuthorRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ElasticsearchClient _elasticClient;
    private readonly ILogger<AuthorRepository> _logger;
    //private readonly AudioRecordRepository _audioRecordRepository;

    public AuthorRepository(ApplicationDbContext dbContext, ElasticsearchClient elasticClient,
        ILogger<AuthorRepository> logger)
    {
        _dbContext = dbContext;
        _elasticClient = elasticClient;
        _logger = logger;
        // _audioRecordRepository = audioRecordRepository;
    }

    /// <summary>
    /// Редактируем автора по id (изменится у всех треков)
    /// изменяет его имя в postgres, а дальше у всех документов в elasticsearch
    /// </summary>
    /// <param name="id"></param>
    /// <param name="newName"></param>
    public async Task EditAuthorName(int id, string newName)
    {
        var author = await _dbContext.Authors.FirstOrDefaultAsync(a => a.Id == id);
        if (author != null)
        {
            // 1. Обновляем записи в PostgreSQL
            var oldName =
                author.Name; // Сохраняем старое имя на случай, если оно понадобится для логирования или сложной логики
            author.Name = newName;
            await _dbContext.SaveChangesAsync(); // Сначала применяем изменения в БД

            // 2. Обновляем соответствующие записи в Elasticsearch
            // Продолжаем, только если имя действительно изменилось, чтобы избежать ненужных обновлений ES
            if (oldName != newName)
            {
                _logger.LogInformation(
                    "Имя автора изменено с '{OldName}' на '{NewName}' для ID {AuthorId}. Распространяется в Elasticsearch.",
                    oldName, newName, id);
                try
                {
                    // Используем UpdateByQuery для обновления всех документов, где AuthorId совпадает
                    var updateByQueryResponse = await _elasticClient.UpdateByQueryAsync<AudioRecordForElastic>(u => u
                        .Query(q => q
                            .Term(t => t // Используем Term-запрос для точного совпадения по числовым/keyword полям
                                    .Field(f => f.AuthorId) // Поле в AudioRecordForElastic
                                    .Value(id) // ID автора для совпадения
                            )
                        )
                        .Script(s => s
                                // Скрипт для обновления поля AuthorName
                                // 'ctx._source' ссылается на обновляемый документ
                                // 'params.newName' - это параметр, передаваемый в скрипт
                                .Source(
                                    "ctx._source.authorName = params.newName") // Обратите внимание на имя поля в ES (вероятно, 'authorName')
                                .Lang("painless") // Язык скриптов Elasticsearch по умолчанию
                                .Params(p => p.Add("newName", newName)) // Передаем новое имя как параметр
                        )
                    );

                    if (!updateByQueryResponse.IsValidResponse)
                    {
                        _logger.LogError("Не удалось обновить имя автора в Elasticsearch для Author ID {AuthorId}. " +
                                         "Причина: {Reason}. DebugInfo: {DebugInfo}",
                            id,
                            updateByQueryResponse.ElasticsearchServerError?.Error?.Reason,
                            updateByQueryResponse.DebugInformation);
                        // Здесь вы можете выбросить исключение, реализовать механизм повторных попыток
                        // или пометить это для последующего согласования.
                        // throw new Exception($"Ошибка обновления Elasticsearch: {updateByQueryResponse.ServerError?.Error?.Reason}");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Успешно обновлено {UpdatedCount} документов в Elasticsearch для Author ID {AuthorId} с новым именем '{NewName}'. " +
                            "Неудач: {FailuresCount}",
                            updateByQueryResponse.Updated, id, newName, updateByQueryResponse.Failures.Count);
                        if (updateByQueryResponse.Failures.Any())
                        {
                            foreach (var failure in updateByQueryResponse.Failures)
                            {
                                _logger.LogWarning(
                                    "Ошибка обновления Elasticsearch для документа ID {DocId} в индексе {Index}: {Cause}",
                                    failure.Id, failure.Index, failure.Cause.Reason);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Произошло исключение при обновлении Elasticsearch для Author ID {AuthorId}.",
                        id);
                    // Обработайте или перебросьте исключение в зависимости от стратегии обработки ошибок вашего приложения
                }
            }
            else
            {
                _logger.LogInformation(
                    "Имя автора для ID {AuthorId} не изменилось ('{NewName}'). Обновление Elasticsearch пропущено.", id,
                    newName);
            }
        }
        else
        {
            _logger.LogWarning("Автор с ID {AuthorId} не найден для редактирования.", id);
        }
    }
}