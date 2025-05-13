namespace AuthService.Models.DTO;

public class RegisterModel
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    
    public required string Name { get; set; }
}