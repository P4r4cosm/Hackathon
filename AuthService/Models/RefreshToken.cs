namespace AuthService.Models;

public class RefreshToken
{
    public Guid Id { get; set; } // Или Guid
    public string UserId { get; set; }
    public ApplicationUser User { get; set; } // Navigation property
    public string Token { get; set; } // Сам refresh token (уникальная строка)
    public DateTime Expires { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Revoked { get; set; } // Дата отзыва
    public string? ReplacedByToken { get; set; } // Для отслеживания ротации

    public bool IsExpired => DateTime.UtcNow >= Expires;
    public bool IsRevoked => Revoked != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}