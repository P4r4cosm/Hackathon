namespace SoundService.Models;

public class Keyword
{
    public Guid Id { get; set; }
    public string Text { get; set; }

    public virtual ICollection<AudioKeyword> AudioKeywords { get; set; }
}