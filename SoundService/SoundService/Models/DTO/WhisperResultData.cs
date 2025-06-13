using System.Text.Json.Serialization;

namespace SoundService.Models;

public class WhisperResultData : TaskResultBase
{
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