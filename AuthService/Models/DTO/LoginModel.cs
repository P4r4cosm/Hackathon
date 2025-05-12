namespace AuthService.Models.DTO;

public class LoginModel
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}