namespace SoundService.Models;

public class AudioMetadata
{
    public string Title { get; set; }
    public string Artist { get; set; }
    public int? Year { get; set; }
    public string Album { get; set; }
    public string Genre { get; set; }
    public TimeSpan Duration { get; set; }
    public int Bitrate { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }

    // Распознанный текст (транскрипт)
    public string Transcript { get; set; }
    // Сегменты с таймингами
    public List<TranscriptSegment> TranscriptSegments { get; set; } = new();
    // Ключевые слова
    public List<string> Keywords { get; set; } = new();

    // Тематические теги (патриотизм, победа, тоска и т.д.)
    public List<string> ThematicTags { get; set; } = new();

   
    // Статус модерации (например, "ожидает", "утверждено")
    public string ModerationStatus { get; set; } = "pending";
}