namespace SoundService.Models;

public class AudioKeyword
{
    public Guid AudioRecordId { get; set; }
    public AudioRecord AudioRecord { get; set; }

    public Guid KeywordId { get; set; }
    public Keyword Keyword { get; set; }
}