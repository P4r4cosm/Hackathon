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
    
    
   

    [HttpPost("transcript")]
    public async Task<IActionResult> Transcipt(string path)
    {
        
        var demucsTaskData = new DemucsTaskData
        {
            TaskId = Guid.NewGuid().ToString(),
            MinioFilePath = path,
        };
        
        
        await _rabbitMQService.PublishDemucsTask(demucsTaskData);
        
        
        return Ok("Сообщение отправлено");
    }
   
}