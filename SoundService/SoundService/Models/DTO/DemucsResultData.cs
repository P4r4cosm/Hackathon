using System.Text.Json.Serialization;

namespace SoundService.Models;

public class DemucsResultData:  TaskResultBase
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } // "demucs"

    [JsonPropertyName("output_bucket_name")]
    public string OutputBucketName { get; set; }

    [JsonPropertyName("output_object_name")]
    public string OutputObjectName { get; set; }
    
    // ДОБАВЛЕНО: Путь к исходному файлу, который был обработан Demucs
    [JsonPropertyName("original_input_object")]
    public string OriginalInputObject { get; set; }
}