namespace SoundService.Models;

public class AudioRecordForElastic
{
    public Guid Id { get; set; }
    public string Title { get; set; }                // Название песни
    public ICollection<TranscriptSegment> TranscriptSegments { get; set; }
    public string FullText { get; set; }
}