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
                await _audioMetadataService.CreateAudioRecordFromMetadata(tempFilePath, file.FileName, filePathInMinio, data);
            
            // Обновляем метаданные если они указаны в запросе
            if (!string.IsNullOrEmpty(data.Title))
                audioRecord.Title = data.Title;
            if (!string.IsNullOrEmpty(data.AlbumName))
                audioRecord.Album.Title = data.AlbumName; 
            if (!string.IsNullOrEmpty(data.ArtistName))
                audioRecord.Author.Name = data.ArtistName;
            if (data.Year.HasValue)
                audioRecord.Year = data.Year.Value;
            
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

    [HttpGet("authors")]
    public async Task<IActionResult> GetAllAuthors()
    {
        return Ok(await _audioRecordRepository.GetAllAuthorsAsync());
    }

    [HttpGet("genres")]
    public async Task<IActionResult> GetAllGenres()
    {
        return Ok(await _audioRecordRepository.GetAllGenresAsync());
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetAllTags()
    {
        return Ok(await _audioRecordRepository.GetAllThematicTagsAsync());
    }

    [HttpGet("keywords")]
    public async Task<IActionResult> GetAllKeywords()
    {
        return Ok(await _audioRecordRepository.GetAllKeywordsAsync());
    }

    [HttpGet("author_tracks")]
    public async Task<IActionResult> GetAllAuthorsTracks(int id, int from, int count)
    {
        return Ok(await _audioRecordRepository.GetTracksByAuthor(id, from, count));
    }
    
    [HttpGet("year_tracks")]
    
    public async Task<IActionResult> GetAllYearTracks(int year, int from, int count)
    {
        return Ok(await _audioRecordRepository.GetTracksByYear(year, from, count));
    }
    
    [HttpGet("genres_tracks")]
    public async Task<IActionResult> GetAllGenresTracksById(int id, int from, int count)
    {
        return Ok(await _audioRecordRepository.GetTracksByGenreId(id, from, count));
    }
    
    
    [HttpPost("tag_tracks")]
    public async Task<IActionResult> GetAllTagTracks(IEnumerable<string> tags, int from, int count)
    {
        return Ok(await _audioRecordRepository.GetTracksByThematicTags(tags, from, count));
    }
    [HttpPost("keyword_tracks")]
    public async Task<IActionResult> GetAllKeywordTracks(IEnumerable<string> keywords, int from, int count)
    {
        return Ok(await _audioRecordRepository.GetTracksByKeywords(keywords, from, count));
    }
}