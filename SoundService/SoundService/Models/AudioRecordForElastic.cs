
using System.Text.Json.Serialization;

namespace SoundService.Models;

public class AudioRecordForElastic
{
    
    public int Id { get; set; }
    public string Title { get; set; }                // Название песни
    public ICollection<TranscriptSegment> TranscriptSegments { get; set; }
    public string FullText { get; set; }
    
    public string Path { get; set; }
    public string AuthorName { get; set; }
    public int? AuthorId { get; set; }              // Если нужна фильтрация по ID автора
    public string AlbumTitle { get; set; }          // Если нужен поиск по альбому
    public int? AlbumId {get; set;}
    public int? Year { get; set; }
    public List<GenreDto> Genres { get; set; }
    public List<string> ThematicTags { get; set; }   // Список названий тегов ["патриотические", "победа"]
    public List<string> Keywords { get; set; }       // Из AudioKeywords, если они тоже участвуют в поиске
    
    public ModerationState ModerationStatus { get; set; }     // Например, "Approved", "Pending"
    
    public TimeSpan Duration { get; set; }
    public DateTime UploadedAt { get; set; }         // Для сортировки по дате добавления
}