using System.Reflection.PortableExecutable;
using Microsoft.AspNetCore.Mvc;
using SoundService.Models;
using SoundService.Repositories;
using SoundService.Services;

namespace SoundService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AudioController : ControllerBase
{
    private readonly AudioMetadataService _audioMetadataService;
    private readonly AudioRecordRepository _audioRecordRepository;
    private readonly ILogger<AudioController> _logger;
    private readonly MinIOService _minIOService;
    private readonly AudioRestoreService _audioRestoreService;
    private readonly SpeechToTextService _speechToTextService;
    private readonly TextAnalysisService _textAnalysisService;

    public AudioController(
        AudioMetadataService audioMetadataService, 
        AudioRecordRepository audioRecordRepository,
        ILogger<AudioController> logger, 
        MinIOService minIOService,
        AudioRestoreService audioRestoreService,
        SpeechToTextService speechToTextService,
        TextAnalysisService textAnalysisService)
    {
        _audioMetadataService = audioMetadataService;
        _audioRecordRepository = audioRecordRepository;
        _logger = logger;
        _minIOService = minIOService;
        _audioRestoreService = audioRestoreService;
        _speechToTextService = speechToTextService;
        _textAnalysisService = textAnalysisService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadAudio(IFormFile file, string folderToUpload = "uploads")
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не загружен.");

        _logger.LogInformation("Начало загрузки файла: {FileName}, размер: {Size} байт, папка: {Folder}",
            file.FileName, file.Length, folderToUpload);

        // Получаем расширение файла
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        // Сохраняем файл во временный каталог или обрабатываем напрямую
        var tempFilePath = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), ext));

        using (var stream = new FileStream(tempFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var filePathInMinio = folderToUpload + "/" + file.FileName;
        _logger.LogInformation("Файл сохранен во временную директорию: {TempPath}, будет загружен в MinIO: {MinioPath}",
            tempFilePath, filePathInMinio);

        try
        {
            // Загружаем оригинальный файл в MinIO
            await _minIOService.UploadTrackAsync(tempFilePath, filePathInMinio, file.ContentType);
            _logger.LogInformation("Файл успешно загружен в MinIO");

            // Получаем метаданные из файла
            var metadata = await _audioMetadataService.CreateAudioRecordFromMetadata(tempFilePath, file.FileName, filePathInMinio);
            _logger.LogInformation("Metadata: {Metadata}", metadata);

            // Восстанавливаем качество звука
            _logger.LogInformation("Начало процесса улучшения звука");
            var restoredPathInMinio = await _audioRestoreService.RestoreAudioQualityAsync(tempFilePath, filePathInMinio);
            metadata.RestoredFilePath = restoredPathInMinio;
            _logger.LogInformation("Звук улучшен, путь: {RestoredPath}", restoredPathInMinio);

            // Распознаем текст из аудио
            _logger.LogInformation("Начало распознавания текста");
            var (fullText, segments) = await _speechToTextService.RecognizeTextAsync(tempFilePath, metadata.Title);
            _logger.LogInformation("Текст распознан: {TextLength} символов, {SegmentsCount} сегментов", 
                fullText?.Length ?? 0, segments?.Count ?? 0);

            // Анализируем текст для получения тегов и ключевых слов
            _logger.LogInformation("Начало анализа текста");
            var (tags, keywords) = await _textAnalysisService.AnalyzeTextAsync(fullText, metadata.Title);
            _logger.LogInformation("Текст проанализирован: {TagsCount} тегов, {KeywordsCount} ключевых слов", 
                tags?.Count ?? 0, keywords?.Count ?? 0);

            // Добавляем теги и ключевые слова к метаданным
            if (tags != null && tags.Count > 0)
            {
                metadata.AudioThematicTags = tags.Select(t => new AudioThematicTag 
                { 
                    ThematicTag = t,
                    ThematicTagId = t.Id
                }).ToList();
            }

            if (keywords != null && keywords.Count > 0)
            {
                metadata.AudioKeywords = keywords.Select(k => new AudioKeyword 
                { 
                    Keyword = k,
                    KeywordId = k.Id
                }).ToList();
            }

            // Сохраняем метаданные в PostgreSQL
            _logger.LogInformation("Сохранение записи в PostgreSQL");
            await _audioRecordRepository.SaveAsync(metadata);

            // Сохраняем текст и сегменты в Elasticsearch
            var audioRecordElastic = new AudioRecordForElastic
            {
                Id = metadata.Id,
                Title = metadata.Title,
                FullText = fullText,
                TranscriptSegments = segments
            };

            _logger.LogInformation("Сохранение текста в Elasticsearch");
            await _audioRecordRepository.SaveAsync(audioRecordElastic);

            // Удаляем временный файл
            System.IO.File.Delete(tempFilePath);

            return Ok(new { 
                success = true, 
                message = "Запись успешно обработана и сохранена", 
                filePath = filePathInMinio, 
                restoredPath = restoredPathInMinio,
                id = metadata.Id 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке файла: {Message}", ex.Message);
            try
            {
                System.IO.File.Delete(tempFilePath);
            }
            catch {}
            return StatusCode(500, $"Ошибка обработки файла: {ex.Message}");
        }
    }


    [HttpGet("download")]
    public async Task<IActionResult> DownloadAudio(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest("Object name cannot be empty.");
        }

        try
        {
            (Stream fileStream, string contentType, long contentLength) =
                await _minIOService.GetTrackAsync(path, cancellationToken);

            Response.Headers.Append("Accept-Ranges", "bytes");

            var downloadFileName = path;

            return new FileStreamResult(fileStream, contentType ?? "application/octet-stream")
            {
                FileDownloadName = downloadFileName,
                EnableRangeProcessing = true
            };
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "MinIO object not found: {ObjectName}", path);
            return NotFound(ex.Message);
        }
        catch (Minio.Exceptions.MinioException minioEx)
        {
            _logger.LogError(minioEx, "MinIO exception while streaming object: {ObjectName}", path);
            return StatusCode(StatusCodes.Status500InternalServerError, $"MinIO Error: {minioEx.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming object: {ObjectName}", path);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while streaming the file.");
        }
    }

    [HttpGet("tracks")]
    public async Task<IActionResult> GetAllTracks()
    {
        var audioList = await _audioRecordRepository.GetAllAsync();
        return Ok(audioList);
    }

    [HttpGet("track/{id}")]
    public async Task<IActionResult> GetTrackById(int id)
    {
        var audio = await _audioRecordRepository.GetTrackByIdAsync(id);
        return Ok(audio);
    }

    [HttpGet("track_text/{id}")]
    public async Task<IActionResult> GetTrackTextById(int id)
    {
        var audioElastic = await _audioRecordRepository.GetTrackTextByIdAsync(id);
        return Ok(audioElastic);
    }
}