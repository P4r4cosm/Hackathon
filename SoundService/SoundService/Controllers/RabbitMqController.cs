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
    
    
    [HttpPost("demucs_task")]
    public async Task<IActionResult> Test(string path)
    {
        var demucsTaskData = new DemucsTaskData
        {
            TaskId = Guid.NewGuid().ToString(),
            MinioFilePath = path
        };
         await _rabbitMQService.PublishDemucsTask(demucsTaskData);
        return Ok("Сообщение отправлено");
    }

    [HttpPost("transcript")]
    public async Task<IActionResult> Transcipt(string path)
    {
        var whisperTaskData = new WhisperTaskData()
        {
            TaskId = Guid.NewGuid().ToString(),
            input_bucket_name = "audio-bucket",
            input_object_name = path,
            output_minio_folder = "/result/whisper"
        };
        await _rabbitMQService.PublishWhisperTask(whisperTaskData);
        return Ok();
    }
   
}