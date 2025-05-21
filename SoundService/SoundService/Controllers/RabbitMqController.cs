using Microsoft.AspNetCore.Mvc;
using SoundService.Models;
using SoundService.RabbitMQ;

namespace SoundService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RabbitMqController: ControllerBase
{
    private readonly RabbitMqService _rabbitMQService;

    public RabbitMqController(RabbitMqService rabbitMQService)
    {
        _rabbitMQService = rabbitMQService;
    }
    
    
    [HttpGet("test")]
    public async Task<IActionResult> Test()
    {
        var demucsTaskData = new DemucsTaskData
        {
            TaskId = Guid.NewGuid().ToString(),
            MinioFilePath = "original_tracks/01 - Священная  война.flac"
        };
         await _rabbitMQService.PublishDemucsTask(demucsTaskData);
        return Ok("Сообщение отправлено");
    }
   
}