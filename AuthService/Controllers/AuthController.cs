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

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration, ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
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
            Response.Cookies.Append("auth_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddDays(7)
            });
            return Ok(new { Message = "Login successful" });
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

            Response.Cookies.Append("auth_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddDays(7)
            });
            return Ok(new { Message = "Login successful" });
        }

        return Unauthorized(new { Message = "Invalid credentials" });
    }

    [HttpGet("google-login")] // Изменил маршрут для ясности
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        returnUrl = "http://localhost:3000/"; // значение по умолчанию
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
        return Challenge(properties, "Google");
    }

    [HttpGet("external-login-callback")]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/"); // По умолчанию на главную, если returnUrl не указан

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Error loading external login information.");
            // Можно перенаправить на страницу ошибки или снова на логин
            return RedirectToAction(nameof(LoginByName)); // Или какой-то общий метод логина
        }

        // Пытаемся войти с помощью внешнего провайдера (например, если пользователь уже привязывал Google)
        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
            isPersistent: false, bypassTwoFactor: true);

        ApplicationUser? user = null;

        if (signInResult.Succeeded)
        {
            user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user != null)
            {
                _logger.LogInformation("User {Email} logged in successfully via {LoginProvider}.",
                    info.Principal.FindFirstValue(ClaimTypes.Email), info.LoginProvider);
            }
            else
            {
                // Этого не должно произойти, если ExternalLoginSignInAsync успешен
                _logger.LogError("External login succeeded but user not found for {LoginProvider} and {ProviderKey}.",
                    info.LoginProvider, info.ProviderKey);
                return BadRequest(new { Message = "Error during external login." });
            }
        }
        else
        {
            // Если пользователь не имеет логина через этого провайдера, пытаемся его найти по email или создать
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                user = await _userManager.FindByEmailAsync(email);
                if (user == null) // Пользователя нет, создаем нового
                {
                    var userName =
                        info.Principal.FindFirstValue(ClaimTypes.Name) ??
                        email; // Можно выбрать другую логику для UserName
                    // Убедимся что UserName уникален, если он уже занят, можно добавить суффикс
                    var tempUser = await _userManager.FindByNameAsync(userName);
                    if (tempUser != null)
                    {
                        userName = $"{userName}_{Guid.NewGuid().ToString().Substring(0, 4)}";
                    }

                    user = new ApplicationUser
                    {
                        UserName = userName,
                        Email = email,
                        EmailConfirmed = true // Email подтвержден провайдером
                    };
                    var createUserResult = await _userManager.CreateAsync(user);
                    if (createUserResult.Succeeded)
                    {
                        _logger.LogInformation("New user {Email} created via {LoginProvider}.", email,
                            info.LoginProvider);
                        await _userManager.AddToRoleAsync(user, "user"); // Назначаем роль
                    }
                    else
                    {
                        _logger.LogError("Failed to create user {Email} via {LoginProvider}: {Errors}",
                            email, info.LoginProvider,
                            string.Join(", ", createUserResult.Errors.Select(e => e.Description)));
                        return BadRequest(new { Message = "Error creating user.", Errors = createUserResult.Errors });
                    }
                }

                // Привязываем внешний логин к существующему или новому пользователю
                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (addLoginResult.Succeeded)
                {
                    _logger.LogInformation("External login {LoginProvider} added for user {Email}.", info.LoginProvider,
                        user.Email);
                    // Повторно пытаемся войти, теперь логин должен быть привязан
                    signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
                        isPersistent: false, bypassTwoFactor: true);
                    if (!signInResult.Succeeded)
                    {
                        _logger.LogError("Failed to sign in user {Email} via {LoginProvider} after adding login.",
                            user.Email, info.LoginProvider);
                        return BadRequest(new { Message = "Error during external sign-in after linking account." });
                    }
                }
                else
                {
                    _logger.LogError("Failed to add external login {LoginProvider} for user {Email}: {Errors}",
                        info.LoginProvider, user.Email,
                        string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
                    return BadRequest(new
                        { Message = "Error linking external account.", Errors = addLoginResult.Errors });
                }
            }
            else
            {
                _logger.LogWarning("Email claim not found from {LoginProvider}.", info.LoginProvider);
                return BadRequest(new { Message = "Email claim not found from external provider." });
            }
        }

        // На этом этапе пользователь (user) должен быть успешно залогинен (signInResult.Succeeded)
        // и у нас должен быть объект user
        if (user == null) // Дополнительная проверка
        {
            _logger.LogError("User object is null after external login process for {LoginProvider} - {ProviderKey}.",
                info.LoginProvider, info.ProviderKey);
            return BadRequest(new { Message = "Critical error during external login." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles);
        SetAuthCookie(token);

        _logger.LogInformation("JWT token generated and cookie set for user {Email} after {LoginProvider} login.",
            user.Email, info.LoginProvider);
        return Redirect(returnUrl);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync(); // Выход из системы Identity (удаляет cookie аутентификации Identity)
        Response.Cookies.Delete("auth_token",
            new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax }); // Удаляем JWT cookie
        _logger.LogInformation("User logged out.");
        return Ok(new { Message = "Logged out successfully" });
    }

    private void SetAuthCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // В production всегда true. Для локальной разработки с HTTP может потребоваться false, но лучше настроить HTTPS.
            Expires = DateTime.UtcNow.AddDays(7),
            SameSite = SameSiteMode.Lax // Рекомендуется для OAuth коллбэков и в целом
        };
        Response.Cookies.Append("auth_token", token, cookieOptions);
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