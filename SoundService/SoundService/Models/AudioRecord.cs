namespace SoundService.Models;

public class AudioRecord
{
    public int Id { get; set; }
    public string Title { get; set; }                // Название песни
    public int AuthorId { get; set; }               // Внешний ключ на Author
    public Author Author { get; set; }

    public int? AlbumId { get; set; }
    public Album Album { get; set; }

    public int? Year { get; set; }
    public int GenreId { get; set; }
    public  Genre Genre { get; set; }

    public TimeSpan Duration { get; set; }
    public string FilePath { get; set; }             // Путь к файлу
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? UploadUserId { get; set; }         // Кто загрузил

    public ICollection<AudioKeyword> AudioKeywords { get; set; }
    public ICollection<AudioThematicTag> AudioThematicTags { get; set; }
    //public ICollection<TranscriptSegment> TranscriptSegments { get; set; }
    public ModerationStatus ModerationStatus { get; set; }
}