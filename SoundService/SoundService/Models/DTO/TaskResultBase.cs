using System.Text.Json.Serialization;

namespace SoundService.Models;

public class TaskResultBase
{
    [JsonPropertyName("task_id")]
    public string TaskId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }
    
    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; }
}