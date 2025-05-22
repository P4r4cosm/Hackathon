using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Data;
using AuthService.Models;
using AuthService.Models.DTO;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly ApplicationDbContext _context;

    private readonly ITokenService _tokenService;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration, ILogger<AuthController> logger, ITokenService tokenService,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
        _tokenService = tokenService;
        _context = context;
    }
    //Метод для установления Cookies
    private void SetTokensInCookies(string accessToken, DateTime accessTokenExpires, string refreshToken, DateTime refreshTokenExpires)
    {
        Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, 
            Expires = accessTokenExpires,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            Expires = refreshTokenExpires,
            SameSite = SameSiteMode.Lax,
            Path = "/refresh" 
        });
    }
    private void DeleteAuthCookies()
    {
        var cookieOptions = new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/" };
        Response.Cookies.Delete("access_token", cookieOptions);
        cookieOptions.Path = "/refresh"; // Путь для refresh_token
        Response.Cookies.Delete("refresh_token", cookieOptions);
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

    // --- Обновленные методы логина ---
    [HttpPost("loginByEmail")]
    public async Task<IActionResult> LoginByEmail(LoginEmailModel emailModel)
    {
        var user = await _userManager.FindByEmailAsync(emailModel.Email);
        if (user == null)
        {
            return Unauthorized(new { Message = "Invalid credentials" });
        }

        // Используйте CheckPasswordAsync, если не хотите побочных эффектов SignInManager с куками Identity
        var passwordValid = await _userManager.CheckPasswordAsync(user, emailModel.Password);
        if (passwordValid)
        // Или если хотите использовать SignInManager:
        // var result = await _signInManager.CheckPasswordSignInAsync(user, emailModel.Password, lockoutOnFailure: false);
        // if (result.Succeeded)
        {
            await _signInManager.SignOutAsync(); // Очищаем старые сессии Identity, если они были

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _tokenService.GenerateAccessToken(user, roles);
            var accessTokenDuration = TimeSpan.FromMinutes(Convert.ToDouble(_configuration["JwtSettings:AccessTokenDurationInMinutes"]));

            var refreshTokenValue = _tokenService.GenerateRefreshTokenValue();
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenValue,
                Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenDurationInDays"]))
            };
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            SetTokensInCookies(accessToken, DateTime.UtcNow.Add(accessTokenDuration), refreshTokenValue, refreshTokenEntity.Expires);

            _logger.LogInformation("User {Email} logged in successfully via email.", user.Email);
            return Ok(new { Message = "Login successful", UserId = user.Id });
        }

        return Unauthorized(new { Message = "Invalid credentials" });
    }

    [HttpPost("loginByName")]
    public async Task<IActionResult> LoginByName(LoginNameModel nameModel)
    {
        var user = await _userManager.FindByNameAsync(nameModel.Name);
        if (user == null)
        {
            return Unauthorized(new { Message = "Invalid credentials" });
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, nameModel.Password);
        if (passwordValid)
        {
            await _signInManager.SignOutAsync(); // Очищаем старые сессии Identity

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _tokenService.GenerateAccessToken(user, roles);
            var accessTokenDuration = TimeSpan.FromMinutes(Convert.ToDouble(_configuration["JwtSettings:AccessTokenDurationInMinutes"]));

            var refreshTokenValue = _tokenService.GenerateRefreshTokenValue();
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenValue,
                Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenDurationInDays"]))
            };
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            SetTokensInCookies(accessToken, DateTime.UtcNow.Add(accessTokenDuration), refreshTokenValue, refreshTokenEntity.Expires);
            
            _logger.LogInformation("User {UserName} logged in successfully by name.", user.UserName);
            return Ok(new { Message = "Login successful", UserId = user.Id });
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

        if (user != null) // Убедитесь, что user не null
        {
            await _signInManager.SignOutAsync(); // Очищаем сессию Identity от внешнего провайдера

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _tokenService.GenerateAccessToken(user, roles);
            var accessTokenDuration = TimeSpan.FromMinutes(Convert.ToDouble(_configuration["JwtSettings:AccessTokenDurationInMinutes"]));

            var refreshTokenValue = _tokenService.GenerateRefreshTokenValue();
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenValue,
                Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenDurationInDays"]))
            };
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            SetTokensInCookies(accessToken, DateTime.UtcNow.Add(accessTokenDuration), refreshTokenValue, refreshTokenEntity.Expires);

            _logger.LogInformation("User {Email} logged in via {LoginProvider}. JWTs set in cookies. Redirecting to {FrontendRedirectUrl}",
                user.Email, info?.LoginProvider ?? "UnknownProvider", frontendRedirectUrl);
            return Redirect(frontendRedirectUrl);
        }
        else
        {
            _logger.LogError("User object is null at the end of external login process for {LoginProvider}. This should not happen.", info?.LoginProvider ?? "UnknownProvider");
            return Redirect(frontendRedirectUrl + (frontendRedirectUrl.Contains("?") ? "&" : "?") + "error=critical_external_login_error");
        }
    }
     [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var oldRefreshTokenValue))
        {
            _logger.LogWarning("Refresh endpoint called without refresh_token cookie.");
            return Unauthorized(new { Message = "Refresh token not found." });
        }

        var storedRefreshToken = await _context.RefreshTokens
            .Include(rt => rt.User) // Важно, чтобы получить пользователя для генерации нового access token'а
            .FirstOrDefaultAsync(rt => rt.Token == oldRefreshTokenValue);

        if (storedRefreshToken == null)
        {
            _logger.LogWarning("Refresh token from cookie not found in DB: {RefreshToken}", oldRefreshTokenValue);
            DeleteAuthCookies(); // Если токен не найден, удаляем куки на всякий случай
            return Unauthorized(new { Message = "Invalid refresh token." });
        }

        if (!storedRefreshToken.IsActive)
        {
            _logger.LogWarning("Inactive refresh token used. Token: {RefreshToken}, Expires: {Expires}, Revoked: {Revoked}",
                storedRefreshToken.Token, storedRefreshToken.Expires, storedRefreshToken.Revoked);
            // (Опционально, но рекомендуется для безопасности) Если кто-то пытается использовать неактивный токен,
            // можно отозвать все активные токены этого пользователя.
            // await RevokeAllUserRefreshTokens(storedRefreshToken.UserId, "Attempted use of inactive token");
            DeleteAuthCookies();
            return Unauthorized(new { Message = "Refresh token has expired or been revoked." });
        }

        var user = storedRefreshToken.User;
        if (user == null) // Дополнительная проверка
        {
             _logger.LogError("User not found for active refresh token. UserId: {UserId}", storedRefreshToken.UserId);
             DeleteAuthCookies();
             return Unauthorized(new { Message = "User associated with refresh token not found." });
        }

        // --- Refresh Token Rotation (Рекомендуется) ---
        var newRefreshTokenValue = _tokenService.GenerateRefreshTokenValue();
        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshTokenValue,
            Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:RefreshTokenDurationInDays"]))
        };
        _context.RefreshTokens.Add(newRefreshTokenEntity);

        storedRefreshToken.Revoked = DateTime.UtcNow;
        storedRefreshToken.ReplacedByToken = newRefreshTokenValue; // Для отслеживания
        // ----------------------------------------------

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
        var newAccessTokenDuration = TimeSpan.FromMinutes(Convert.ToDouble(_configuration["JwtSettings:AccessTokenDurationInMinutes"]));

        SetTokensInCookies(newAccessToken, DateTime.UtcNow.Add(newAccessTokenDuration), newRefreshTokenValue, newRefreshTokenEntity.Expires);

        await _context.SaveChangesAsync(); // Сохраняем изменения (включая отзыв старого RT и добавление нового)

        _logger.LogInformation("Token refreshed successfully for user {UserId}.", user.Id);
        return Ok(new { Message = "Token refreshed successfully" });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue("refresh_token", out var refreshTokenValue))
        {
            var storedRefreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue);

            if (storedRefreshToken != null && storedRefreshToken.IsActive)
            {
                storedRefreshToken.Revoked = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Refresh token revoked for user {UserId} on logout.", storedRefreshToken.UserId);
            }
        }
        // await _signInManager.SignOutAsync(); // Это удаляет куки Identity, если они есть.
        // В нашей схеме с JWT в куках, это не так критично, но не повредит.
        DeleteAuthCookies();
        _logger.LogInformation("User logged out and auth cookies deleted.");
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