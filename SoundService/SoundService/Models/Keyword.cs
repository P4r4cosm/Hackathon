namespace SoundService.Models;

public class Keyword
{
    public int Id { get; set; }
    public string Text { get; set; }

    public virtual ICollection<AudioKeyword> AudioKeywords { get; set; }
}