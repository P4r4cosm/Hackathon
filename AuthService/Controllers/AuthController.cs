using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Models;
using AuthService.Models.DTO;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    // Хардкодные учетные записи
    private readonly Dictionary<string, (string password, string[] roles)> _hardcodedUsers = new()
    {
        { "admin@admin.com", ("Admin_s3cret_p@ssw0rd", new[] { "admin" }) },
        { "user1@example.com", ("Password123!", new[] { "user" }) },
        { "user2@example.com", ("Password123!", new[] { "user" }) }
    };

    public AuthController(
        UserManager<ApplicationUser> userManager, 
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    public IActionResult Register(RegisterModel model)
    {
        // В захардкоженном режиме регистрация "всегда успешна"
        _logger.LogInformation("Registered user (hardcoded): {Email}", model.Email);
        return Ok(new { Message = "User registered successfully" });
    }

    [HttpPost("loginByEmail")]
    public IActionResult LoginByEmail(LoginEmailModel emailModel)
    {
        _logger.LogInformation("Login attempt by email: {Email}", emailModel.Email);
        
        // Проверяем захардкоженные учетные записи
        if (_hardcodedUsers.TryGetValue(emailModel.Email, out var userInfo) && 
            userInfo.password == emailModel.Password)
        {
            _logger.LogInformation("Login successful for: {Email}", emailModel.Email);
            
            var token = GenerateJwtToken(emailModel.Email, userInfo.roles);
            
            Response.Cookies.Append("auth_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddDays(7)
            });
            
            return Ok(new { Message = "Login successful", Roles = userInfo.roles });
        }
        
        _logger.LogWarning("Login failed for: {Email}", emailModel.Email);
        return Unauthorized(new { Message = "Invalid credentials" });
    }
    
    [HttpPost("loginByName")]
    public IActionResult LoginByName(LoginNameModel nameModel)
    {
        _logger.LogInformation("Login attempt by name: {Name}", nameModel.Name);
        
        // Для этого метода просто попробуем найти пользователя с такой почтой
        // (для простоты предположим, что имя = email)
        if (_hardcodedUsers.TryGetValue(nameModel.Name, out var userInfo) && 
            userInfo.password == nameModel.Password)
        {
            _logger.LogInformation("Login successful for: {Name}", nameModel.Name);
            
            var token = GenerateJwtToken(nameModel.Name, userInfo.roles);
            
            Response.Cookies.Append("auth_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddDays(7)
            });
            
            return Ok(new { Message = "Login successful", Roles = userInfo.roles });
        }
        
        _logger.LogWarning("Login failed for: {Name}", nameModel.Name);
        return Unauthorized(new { Message = "Invalid credentials" });
    }

    private string GenerateJwtToken(string email, string[] roles)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"];
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        //если указано время в .env, то устанавливаем его, иначе время жизни токена - час
        int durationInHours = int.TryParse(jwtSettings["DurationInHours"], out int res) ? res : 1;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email)
        };
        
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(durationInHours), // Время жизни токена
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}