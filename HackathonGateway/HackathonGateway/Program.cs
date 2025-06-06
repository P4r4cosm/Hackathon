using System.Security.Claims;
using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

Env.Load();
var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//конфигурируем Kestrel (добавляем сертификат)
KestrelConfiguratorHelper.ConfigureKestrel(builder);


services.AddOpenApi();
services.AddAuthorization();

//конфигурация для JWT
var configuration = builder.Configuration;
var jwtSettings = configuration.GetSection("JwtSettings");
var secretKey =jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
// 1. Настройка Аутентификации JWT
services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction(); // true в production
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            RoleClaimType = ClaimTypes.Role, 
            ClockSkew = TimeSpan.Zero
        };
        // Чтение токена из куки
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("access_token", out var tokenFromCookie))
                {
                    context.Token = tokenFromCookie;
                }

                return Task.CompletedTask;
            }
        };
    });
// 2. Настройка Авторизации (Политики)
services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin")); // Требует роль "Admin"

    options.AddPolicy("UserOrAdminAccess", policy =>
        policy.RequireRole("user", "admin")); // Требует роль "User" ИЛИ "Admin"
});
// 3. Добавление и конфигурация YARP
services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
   
services.AddControllers();

// Добавление и конфигурация CORS

services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {

        // Получаем список разрешенных источников из конфигурации или используем значения по умолчанию
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        
        if (allowedOrigins != null && allowedOrigins.Length > 0)
        {
            policyBuilder
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
        else
        {
            // Для разработки, если ничего не настроено
            if (builder.Environment.IsDevelopment())
            {
                policyBuilder
                    .WithOrigins(
                        "http://localhost:3010", 
                        "http://localhost:3000",
                        "http://127.0.0.1:3010",
                        "http://127.0.0.1:3000"
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
            else
            {
                // Если это продакшн и нет настроек - ограничиваем доступ максимально
                policyBuilder
                    .WithOrigins("https://" + builder.Configuration["Kestrel:Endpoints:Https:Host"] ?? "localhost")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
        }
        policyBuilder
            .WithOrigins("https://localhost:3000") // URL вашего фронтенда
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Важно для передачи кук
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Регистрирует конечную точку /openapi/v1.json
    // Добавляем Swagger UI
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Gateway API");
        options.RoutePrefix = ""; // Доступ по /swagger
    });
}
app.UseCors("CorsPolicy"); // Применить CORS

// app.UseHttpsRedirection(); // Отключаем автоматический редирект на HTTPS

// Важно: Аутентификация и Авторизация ДО YARP
app.UseAuthentication();

app.UseAuthorization();
// 3. Используйте YARP
app.MapReverseProxy();
app.MapControllers(); 
app.Run();