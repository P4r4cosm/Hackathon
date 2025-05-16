namespace SoundService.Models;

public class Album
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public int? Year { get; set; }

    public ICollection<AudioRecord> Tracks { get; set; }
}