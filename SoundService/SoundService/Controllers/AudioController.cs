using System.Reflection.PortableExecutable;
using Microsoft.AspNetCore.Mvc;
using SoundService.Infrastucture;
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
    private MinIOService _minIOService;

    public AudioController(AudioMetadataService audioMetadataService, AudioRecordRepository audioRecordRepository,
        ILogger<AudioController> logger, MinIOService minIOService)
    {
        _audioMetadataService = audioMetadataService;
        _audioRecordRepository = audioRecordRepository;
        _logger = logger;
        _minIOService = minIOService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadAudio(UploadAudioDto data)
    {
        var file = data.File;
        if (file == null || file.Length == 0)
            return BadRequest("Файл не загружен.");

        // Получаем расширение файла
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        // Сохраняем файл во временный каталог или обрабатываем напрямую
        var tempFilePath = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), ext));

        using (var stream = new FileStream(tempFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var filePathInMinio = data.FolderToUpload + "/" + file.FileName;

        await _minIOService.UploadTrackAsync(tempFilePath, filePathInMinio, file.ContentType);

        try
        {
            //достаём метаданные
            var audioRecord =
                await _audioMetadataService.CreateAudioRecordFromMetadata(tempFilePath, file.FileName, filePathInMinio);
            _logger.LogInformation("Metadata: {Metadata}", audioRecord);
            //сохраняем трек в postgres и в elastic
            _logger.LogInformation("Saving record to postgres...");
            await _audioRecordRepository.SaveAsync(audioRecord);
            var audioRecordElastic = AudioRecordConverter.ToAudioRecordForElastic(audioRecord);
            
            audioRecordElastic.FullText = "Не обработан";
            audioRecordElastic.TranscriptSegments = new List<TranscriptSegment>()
            {
                new TranscriptSegment()
                {
                    Start = 0,
                    End = 0,
                    Text = "Не обработан"
                }
            };
            
            _logger.LogInformation("Saving record to elastic...");
            await _audioRecordRepository.SaveAsync(audioRecordElastic);
            //удаляем временный файл
            System.IO.File.Delete(tempFilePath);
            return Ok("Record saved successfully.");
        }
        catch (Exception ex)
        {
            System.IO.File.Delete(tempFilePath);
            return StatusCode(500, $"Ошибка сохранения файла: {ex.Message}");
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
    public async Task<IActionResult> GetAllTracks(int from, int count)
    {
        //var audioList = await _audioRecordRepository.GetAllAsync();
        var audioList = await _audioRecordRepository.GetAllAsync(from, count);
        return Ok(audioList);
    }

    [HttpGet("track/{id}")]
    public async Task<IActionResult> GetTrackById(int id)
    {
        var audio = await _audioRecordRepository.GetTrackTextByIdAsync(id);
        return Ok(audio);
    }

    [HttpGet("track_text/{id}")]
    public async Task<IActionResult> GetTrackTextById(int id)
    {
        var audioElastic = await _audioRecordRepository.GetTrackTextByIdAsync(id);
        return Ok(audioElastic);
    }
}