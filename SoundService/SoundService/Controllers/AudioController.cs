using Microsoft.AspNetCore.Mvc;
using SoundService.Services;

namespace SoundService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AudioController: ControllerBase
{
    private readonly AudioMetadataService _audioMetadataService;
    public AudioController(AudioMetadataService audioMetadataService)
    {
        _audioMetadataService=audioMetadataService;
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
            var metadata = _audioMetadataService.ExtractMetadata(tempFilePath, file.FileName);
            System.IO.File.Delete(tempFilePath);
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            System.IO.File.Delete(tempFilePath);
            return StatusCode(500, $"Ошибка обработки файла: {ex.Message}");
        }
    }
}