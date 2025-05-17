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

    public AudioController(AudioMetadataService audioMetadataService, AudioRecordRepository audioRecordRepository,
        ILogger<AudioController> logger)
    {
        _audioMetadataService = audioMetadataService;
        _audioRecordRepository = audioRecordRepository;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadAudio(IFormFile file)
    {
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

        try
        {
            //достаём метаданные
            var metadata = await _audioMetadataService.CreateAudioRecordFromMetadata(tempFilePath, file.FileName);
            _logger.LogInformation("Metadata: {Metadata}", metadata);

            //забрасываем трек в сервис, улучшающий звук

            //забрасываем улучшенный трек в сервис, достающий вокал

            //забрасываем трек в нейронку и достаём текст с таймкодами

            //забрасываем текст/трек для получения ключевых слов

            //забрасываем текст/трек для получения тегов

            //сохраняем трек в postgres и в elastic
            _logger.LogInformation("Saving record to postgres...");
            await _audioRecordRepository.SaveAsync(metadata);
            var audioRecordElastic = new AudioRecordForElastic
            {
                Id = metadata.Id,
                Title = metadata.Title,
                FullText = "test",
                TranscriptSegments = new List<TranscriptSegment>()
                {
                    new TranscriptSegment()
                    {
                        Start = 0,
                        End = 10,
                        Text = "test"
                    }
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
            return StatusCode(500, $"Ошибка обработки файла: {ex.Message}");
        }
    }
}