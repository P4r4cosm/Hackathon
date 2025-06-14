using SoundService.Abstractions;
using SoundService.Models;
using SoundService.RabbitMQ;
using SoundService.Repositories;

namespace SoundService.Services;

public class TaskResultHandler : ITaskResultHandler
{
    private readonly ILogger<TaskResultHandler> _logger;
    private readonly RabbitMqService _rabbitMQService;
    private readonly AudioRecordRepository _audioRecordRepository;
    public TaskResultHandler(ILogger<TaskResultHandler> logger, RabbitMqService rabbitMQService
    , AudioRecordRepository audioRecordRepository)
    {
        _logger = logger;
        _rabbitMQService = rabbitMQService;
        _audioRecordRepository = audioRecordRepository;
    }

    /// <summary>
    /// Отслеживает результат Demucs, срабатывает как только приходит задача в очередь от
    /// Demucs, запускает задачу для Whisper
    /// </summary>
    /// <param name="result"></param>
    public async Task HandleDemucsResultAsync(DemucsResultData result)
    {
        _logger.LogInformation("Получен успешный результат от Demucs для TaskId: {TaskId}. Файл: s3://{Bucket}/{Object}",
            result.TaskId, result.OutputBucketName, result.OutputObjectName);
        
        var whisperTaskData = new WhisperTaskData()
        {
            TaskId = Guid.NewGuid().ToString(),
            input_bucket_name = "audio-bucket",
            input_object_name = result.OriginalInputObject,
            output_minio_folder = "/result/whisper"
        };
        await _rabbitMQService.PublishWhisperTask(whisperTaskData);
        // TODO: Обновить статус задачи в базе данных
       
    }
    /// <summary>
    /// Записывает изменения в Elastic для всех записей, ссылающихся на трек по указанному пути
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public async Task HandleWhisperResultAsync(WhisperResultData result)
    {
        var path = result.InputObject;
        _logger.LogInformation("Получен успешный результат от Whisper для TaskId: {TaskId}. Текст: {Text}. Изначальный файл {Path}",
            result.TaskId, result.FullText, path);
        var segments = new List<TranscriptSegment>();
        foreach (var segment in result.Segments)
        {
            var Newsegment = new TranscriptSegment()
            {
                Start = segment.Start,
                End = segment.End,
                Text = segment.Text
            };
            segments.Add(Newsegment);
        }
        await _audioRecordRepository.EditAudioRecordAsyncByPath(path, result.FullText, segments);
        // TODO: Сохранить транскрипцию в базе данных
        
    }

    public Task HandleFailedResultAsync(TaskResultBase result, string originalMessage)
    {
        _logger.LogError("Получен результат с ошибкой для TaskId: {TaskId}. Ошибка: {Error}. Сообщение: {OriginalMessage}",
            result.TaskId, result.ErrorMessage, originalMessage);
        return Task.CompletedTask;
    }
}