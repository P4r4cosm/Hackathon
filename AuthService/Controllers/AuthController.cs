using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Models;
using AuthService.Models.DTO;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
    private readonly IOptionsMonitor<GoogleOptions> _googleOptionsMonitor;

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

    [HttpGet("google-login")]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var redirectUrlAfterAuthServiceProcessing =
            returnUrl ?? Url.Content("~/"); // URL для редиректа ПОСЛЕ обработки в ExternalLoginCallback

        _logger.LogInformation(
            "Initiating Google login. Apparent Request Scheme: {Scheme}, Apparent Request Host: {Host}. Redirect after processing: {ReturnUrl}",
            Request.Scheme, Request.Host.ToString(), redirectUrlAfterAuthServiceProcessing);

        var properties = new AuthenticationProperties { RedirectUri = "http://localhost:3000" };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("external-login-callback")]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        // returnUrl здесь - это то, что было установлено в AuthenticationProperties.RedirectUri
        // при вызове Challenge() в GoogleLogin или GitHubLogin. Это URL вашего фронтенда.
        var frontendRedirectUrl = returnUrl ??
                                  _configuration["FrontendDefaults:LoginSuccessRedirectUrl"] ??
                                  "http://localhost:3000"; // URL фронтенда

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning(
                "Error loading external login information. Redirecting to frontend: {FrontendRedirectUrl}",
                frontendRedirectUrl);
            return Redirect(frontendRedirectUrl + (frontendRedirectUrl.Contains("?") ? "&" : "?") +
                            "error=external_login_info_error");
        }

        _logger.LogInformation("External login callback for provider {LoginProvider}. User Principal Name: {Name}",
            info.LoginProvider, info.Principal.Identity?.Name);

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
            isPersistent: false, bypassTwoFactor: true);

        ApplicationUser? user = null;

        if (signInResult.Succeeded)
        {
            user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user != null)
            {
                _logger.LogInformation("User {Email} logged in successfully via {LoginProvider}.",
                    user.Email ?? info.Principal.FindFirstValue(ClaimTypes.Email),
                    info.LoginProvider); // Логируем email пользователя
            }
            else
            {
                // Этого не должно произойти, если ExternalLoginSignInAsync успешен
                _logger.LogError("External login succeeded but user not found for {LoginProvider} and {ProviderKey}.",
                    info.LoginProvider, info.ProviderKey);
                return Redirect(frontendRedirectUrl + (frontendRedirectUrl.Contains("?") ? "&" : "?") +
                                "error=external_login_user_not_found_after_success");
            }
        }
        else
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email != null)
            {
                user = await _userManager.FindByEmailAsync(email);
                if (user == null) // Пользователя с таким email нет, создаем нового
                {
                    // Для GitHub, ClaimTypes.Name обычно является логином GitHub.
                    // Это хороший кандидат для UserName.
                    var userName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? // GitHub login
                                   info.Principal.FindFirstValue("urn:github:login") ?? // Явный клейм для GitHub login
                                   email.Split('@')[0]; // Запасной вариант

                    // Обеспечиваем уникальность UserName
                    var tempUser = await _userManager.FindByNameAsync(userName);
                    if (tempUser != null)
                    {
                        // Если такой UserName уже существует, добавляем суффикс
                        userName = $"{userName}_{Guid.NewGuid().ToString().Substring(0, 4)}";
                    }

                    user = new ApplicationUser
                    {
                        UserName = userName, Email = email, EmailConfirmed = true
                    }; // EmailConfirmed = true для внешних провайдеров
                    var createUserResult = await _userManager.CreateAsync(user);
                    if (createUserResult.Succeeded)
                    {
                        _logger.LogInformation(
                            "New user {Email} created via {LoginProvider}. Assigned UserName: {UserName}",
                            email, info.LoginProvider, user.UserName);
                        await _userManager.AddToRoleAsync(user, "user"); // Назначаем роль по умолчанию
                    }
                    else
                    {
                        _logger.LogError("Failed to create user {Email} via {LoginProvider}: {Errors}",
                            email, info.LoginProvider,
                            string.Join(", ", createUserResult.Errors.Select(e => e.Description)));
                        var errorDescriptions = string.Join(";", createUserResult.Errors.Select(e => e.Description));
                        return Redirect(frontendRedirectUrl + (frontendRedirectUrl.Contains("?") ? "&" : "?") +
                                        $"error=user_creation_failed&details={Uri.EscapeDataString(errorDescriptions)}");
                    }
                }

                // Пользователь с таким email существует, привязываем внешний логин
                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (addLoginResult.Succeeded)
                {
                    _logger.LogInformation("External login {LoginProvider} added for existing user {Email}.",
                        info.LoginProvider, user.Email);
                    // Пытаемся войти пользователя снова после привязки
                    signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
                        isPersistent: false, bypassTwoFactor: true);

                    if (!signInResult.Succeeded)
                    {
                        _logger.LogError(
                            "Failed to sign in user {Email} via {LoginProvider} after adding login. SignInResult: {SignInResult}",
                            user.Email, info.LoginProvider, signInResult.ToString());
                        // Это может произойти, если, например, пользователь заблокирован или требует двухфакторную аутентификацию,
                        // которую мы обходим с bypassTwoFactor: true. Но для свежепривязанного логина это маловероятно.
                        return Redirect(frontendRedirectUrl + (frontendRedirectUrl.Contains("?") ? "&" : "?") +
                                        "error=signin_after_link_failed");
                    }
                    // Если вход успешен после привязки, user уже должен быть загружен (FindByNameAsync или FindByLoginAsync)
                    // на предыдущем шаге, если signInResult был Succeeded. Но если мы только что создали user, то он уже есть.
                    // Если мы привязали к существующему user, он тоже есть.
                }
                else
                {
                    _logger.LogError("Failed to add external login {LoginProvider} for user {Email}: {Errors}",
                        info.LoginProvider, user.Email,
                        string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
                    var errorDescriptions = string.Join(";", addLoginResult.Errors.Select(e => e.Description));
                    return Redirect(frontendRedirectUrl + (frontendRedirectUrl.Contains("?") ? "&" : "?") +
                                    $"error=link_external_failed&details={Uri.EscapeDataString(errorDescriptions)}");
                }
            }
            else // Email не получен от внешнего провайдера
            {
                _logger.LogWarning("Email claim not found from {LoginProvider}. User Principal Name: {NameIdentifier}",
                    info.LoginProvider, info.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
                // Это критическая ситуация, если email обязателен.
                // Можно попробовать использовать NameIdentifier для поиска/создания пользователя,
                // но это менее надежно, чем email.
                return Redirect(frontendRedirectUrl + (frontendRedirectUrl.Contains("?") ? "&" : "?") +
                                "error=email_claim_not_found_from_provider");
            }
        }

        if (user == null) // Дополнительная проверка, если user не был установлен
        {
            // Попытка найти пользователя снова, если он был создан, но signInResult не был Succeeded сразу
            if (info.Principal.FindFirstValue(ClaimTypes.Email) != null)
            {
                user = await _userManager.FindByEmailAsync(info.Principal.FindFirstValue(ClaimTypes.Email));
            }

            if (user == null) // Если все еще не нашли
            {
                _logger.LogError(
                    "User object is null after external login process for {LoginProvider} - {ProviderKey}. This should not happen.",
                    info.LoginProvider, info.ProviderKey);
                return Redirect(frontendRedirectUrl + (frontendRedirectUrl.Contains("?") ? "&" : "?") +
                                "error=critical_external_login_error_user_null");
            }
        }

        // На этом этапе user должен быть не null
        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles);
        SetAuthCookie(token); // Используем новый метод для установки cookie

        _logger.LogInformation(
            "JWT token generated and cookie set for user {Email} after {LoginProvider} login. Redirecting to frontend: {FrontendRedirectUrl}",
            user.Email, info.LoginProvider, frontendRedirectUrl);

        return Redirect(frontendRedirectUrl); // Перенаправляем на фронтенд
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


    [HttpGet("github-login")]
    public IActionResult GitHubLogin(string? returnUrl = null)
    {
        var redirectUriForFrontend = returnUrl ?? "http://localhost:3000";

        _logger.LogInformation(
            "Initiating GitHub login. Redirect after processing: {ReturnUrl}",
            redirectUriForFrontend);


        string authenticationScheme = "GitHub";
        var properties = new AuthenticationProperties { RedirectUri = redirectUriForFrontend };
        return Challenge(properties, authenticationScheme);
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