using SoundService.Models;

public class Author
{
    public Guid Id { get; set; }
    public string Name { get; set; }                 

    public ICollection<AudioRecord> Songs { get; set; }
}