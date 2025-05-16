namespace SoundService.Models;

public class ThematicTag
{
    public Guid Id { get; set; }
    public string Name { get; set; } // например: "патриотизм", "победа"

    public virtual ICollection<AudioThematicTag> AudioThematicTags { get; set; }
}