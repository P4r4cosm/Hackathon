namespace SoundService.Models;

public class AudioKeyword
{
    public int AudioRecordId { get; set; }
    public AudioRecord AudioRecord { get; set; }

    public int KeywordId { get; set; }
    public Keyword Keyword { get; set; }
}