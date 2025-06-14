using System.Text.Json.Serialization;

namespace SoundService.Models;

public class DemucsTaskData
{
    public string TaskId { get; set; }
    public string MinioFilePath { get; set; }
    
   
}