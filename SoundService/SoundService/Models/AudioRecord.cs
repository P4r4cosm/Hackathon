namespace SoundService.Models;

public class AudioRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; }                // Название песни
    public Guid ArtistId { get; set; }               // Внешний ключ на Author
    public Author Artist { get; set; }

    public Guid? AlbumId { get; set; }
    public Album Album { get; set; }

    public int? Year { get; set; }
    public Guid GenreId { get; set; }
    public  Genre Genre { get; set; }

    public TimeSpan Duration { get; set; }
    public string FilePath { get; set; }             // Путь к файлу
    public string Format { get; set; }               // WAV / FLAC
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadUserId { get; set; }         // Кто загрузил

    public ICollection<AudioKeyword> Keywords { get; set; }
    public ICollection<AudioThematicTag> ThematicTags { get; set; }
    //public ICollection<TranscriptSegment> TranscriptSegments { get; set; }
    public ModerationStatus ModerationStatus { get; set; }
}