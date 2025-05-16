namespace SoundService.Models;

public class Genre
{
    public Guid Id { get; set; }
    public string Name { get; set; }                 // Например: "Патриотическая", "Рок", "Шансон"

    public ICollection<AudioRecord> Songs { get; set; }
}