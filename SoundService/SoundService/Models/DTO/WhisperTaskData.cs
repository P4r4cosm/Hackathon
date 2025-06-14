using System.Text.Json.Serialization;

namespace SoundService.Models;

public class WhisperTaskData
{
    public string TaskId { get; set; }
    public string input_bucket_name { get; set; }
    public string input_object_name { get; set; }
    public string output_minio_folder { get; set; }
    
    // ДОБАВЛЕНО: Путь к самому первому файлу в цепочке 
    [JsonPropertyName("original_input_object")]
    public string OriginalInputObject { get; set; }
    
}