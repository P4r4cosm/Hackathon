namespace SoundService.Models;

public class AudioThematicTag
{
    public int AudioRecordId { get; set; }
    public AudioRecord AudioRecord { get; set; }

    public int ThematicTagId { get; set; }
    public ThematicTag ThematicTag { get; set; }
}