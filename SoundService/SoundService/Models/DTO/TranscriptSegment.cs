namespace SoundService.Models;

public class TranscriptSegment
{
    public double Start { get; set; } // В секундах
    public double End { get; set; }   // В секундах
    public string Text { get; set; }
}