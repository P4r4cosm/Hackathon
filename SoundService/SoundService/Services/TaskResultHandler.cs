using SoundService.Abstractions;
using SoundService.Models;

namespace SoundService.Services;

public class TaskResultHandler : ITaskResultHandler
{
    private readonly ILogger<TaskResultHandler> _logger;

    public TaskResultHandler(ILogger<TaskResultHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleDemucsResultAsync(DemucsResultData result)
    {
        _logger.LogInformation("Получен успешный результат от Demucs для TaskId: {TaskId}. Файл: s3://{Bucket}/{Object}",
            result.TaskId, result.OutputBucketName, result.OutputObjectName);
        // TODO: Обновить статус задачи в базе данных
        return Task.CompletedTask;
    }

    public Task HandleWhisperResultAsync(WhisperResultData result)
    {
        _logger.LogInformation("Получен успешный результат от Whisper для TaskId: {TaskId}. Текст: {Text}",
            result.TaskId, result.FullText.Substring(0, Math.Min(100, result.FullText.Length)));
        // TODO: Сохранить транскрипцию в базе данных
        return Task.CompletedTask;
    }

    public Task HandleFailedResultAsync(TaskResultBase result, string originalMessage)
    {
        _logger.LogError("Получен результат с ошибкой для TaskId: {TaskId}. Ошибка: {Error}. Сообщение: {OriginalMessage}",
            result.TaskId, result.ErrorMessage, originalMessage);
        // TODO: Обновить статус задачи в базе данных как "Ошибка"
        return Task.CompletedTask;
    }
}