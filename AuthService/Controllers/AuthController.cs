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

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        var user = new ApplicationUser { UserName = model.Name, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            //назначаем всем регистрирующимся user
            await _userManager.AddToRoleAsync(user, "user");
            return Ok(new { Message = "User registered successfully" });
        }

        
        return BadRequest(result.Errors);
    }

    [HttpPost("loginByEmail")]
    public async Task<IActionResult> LoginByEmail(LoginEmailModel emailModel)
    {
        var user = await _userManager.FindByEmailAsync(emailModel.Email);
        if (user == null)
        {
            return Unauthorized(new { Message = "Invalid credentials" });
        }
        var result = await _signInManager.CheckPasswordSignInAsync(user, emailModel.Password, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var token = GenerateJwtToken(user, roles);
            return Ok(new { Token = token });
        }
        return Unauthorized(new { Message = "Invalid credentials" });
    }
    [HttpPost("loginByName")]
    public async Task<IActionResult> LoginByName(LoginNameModel nameModel)
    {
        var result = await _signInManager.PasswordSignInAsync(nameModel.Name, nameModel.Password, isPersistent: false,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByNameAsync(nameModel.Name);
            var role = await _userManager.GetRolesAsync(user!);
            var token = GenerateJwtToken(user!, role);

            return Ok(new { Token = token });
        }
        return Unauthorized(new { Message = "Invalid credentials" });
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
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
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

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