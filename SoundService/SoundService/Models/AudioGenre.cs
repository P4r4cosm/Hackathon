namespace SoundService.Models;

public class AudioGenre
{
    public int AudioRecordId { get; set; }
    public AudioRecord AudioRecord { get; set; }
    
    public int GenreId { get; set; }
    public Genre Genre { get; set; }
}