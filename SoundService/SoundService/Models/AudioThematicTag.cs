namespace SoundService.Models;

public class AudioThematicTag
{
    public Guid AudioRecordId { get; set; }
    public AudioRecord AudioRecord { get; set; }

    public Guid ThematicTagId { get; set; }
    public ThematicTag ThematicTag { get; set; }
}