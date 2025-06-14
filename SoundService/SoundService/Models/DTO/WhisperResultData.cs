using System.Text.Json.Serialization;

namespace SoundService.Models;

public class WhisperResultData : TaskResultBase
{
    // ДОБАВЛЕНО: Путь к ОРИГИНАЛЬНОМУ файлу, который инициировал всю цепочку
    [JsonPropertyName("input_object")]
    public string InputObject { get; set; }
    
    [JsonPropertyName("service")]
    public string Service { get; set; } // "whisper"

    [JsonPropertyName("full_text")]
    public string FullText { get; set; }
    
    [JsonPropertyName("segments")]
    public List<WhisperSegment> Segments { get; set; }
}

public class WhisperSegment
{
    [JsonPropertyName("start")]
    public float Start { get; set; }
    
    [JsonPropertyName("end")]
    public float End { get; set; }
    
    [JsonPropertyName("text")]
    public string Text { get; set; }
}