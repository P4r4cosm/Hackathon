namespace SoundService.Models;

public class ModerationStatus
{
    public Guid Id { get; set; }
    
    public Guid AudioRecordId { get; set; }
    public AudioRecord AudioRecord { get; set; }
    public ModerationState State { get; set; } // pending, approved, rejected
    public string? ModeratorComment { get; set; }
    public string? ModeratorUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public enum ModerationState
{
    Pending,
    Approved,
    Rejected
}