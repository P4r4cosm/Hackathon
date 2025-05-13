using System.Text;
using AuthService.Data;
using AuthService.Extensions;
using AuthService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

Env.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();
// Получаем значения из конфигурации (которая теперь включает .env)
var dbHost = builder.Configuration["POSTGRES_HOST"];
var dbPort = builder.Configuration["POSTGRES_PORT"];
var dbUser = builder.Configuration["POSTGRES_USER"];
var dbPassword = builder.Configuration["POSTGRES_PASSWORD"];
var dbName = builder.Configuration["POSTGRES_DB"];
// проверка на null/empty для всех частей строки подключения
if (string.IsNullOrEmpty(dbHost) || string.IsNullOrEmpty(dbPort) || string.IsNullOrEmpty(dbUser) ||
    string.IsNullOrEmpty(dbPassword) || string.IsNullOrEmpty(dbName))
{
    throw new InvalidOperationException("Одна или несколько переменных окружения для подключения" +
                                        " к PostgreSQL не установлены (POSTGRES_HOST, POSTGRES_PORT, POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB).");
}
// получаем роли
var Roles = builder.Configuration["ROLES"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries);
if (Roles is null || Roles.Count() == 0)
{
    throw new InvalidOperationException("Roles in .env is empty.");
}


//Получаем значения из .env для настройки JWT 
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
// проверка на null/empty всех параметров JWT
if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
{
    throw new InvalidOperationException("Проблема с настройкой JWT (secretKey, issuer, audience).");
}

var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};";

var services = builder.Services;


// 1. Добавление DbContext с использованием строки подключения
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Добавление Identity
services.AddIdentity();

// 3. Добавляем DbSeeder
services.AddScoped<DbSeeder>();

// Добавляем аутентификацию
services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });
// Добавляем авторизацию
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("admin"));
    options.AddPolicy("UserPolicy", policy =>
        policy.RequireRole("user"));
});

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

var app = builder.Build();

// Инициализация ролей
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await seeder.SeedAsync(Roles);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService v1");
        options.RoutePrefix = ""; // Доступ по /
    });
}

app.UseHttpsRedirection();

app.UseRouting(); // <-- Добавляем UseRouting перед Auth


app.UseAuthentication(); // Сначала проверяем, аутентифицирован ли пользователь
app.UseAuthorization(); // Затем проверяем, авторизован ли он для доступа к ресурсу

app.MapControllers(); // Сопоставляем запросы с контроллерами
app.Run();