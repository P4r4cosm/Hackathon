
using System.Text;
using AuthService.Data;
using AuthService.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore; 
using DotNetEnv; 
using Microsoft.IdentityModel.Tokens;

Env.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();
// Получаем значения из конфигурации (которая теперь включает .env)
var dbHost = builder.Configuration["POSTGRES_HOST"];
var dbPort = builder.Configuration["POSTGRES_PORT"]; // Порт обычно строка в конфиге
var dbUser = builder.Configuration["POSTGRES_USER"];
var dbPassword = builder.Configuration["POSTGRES_PASSWORD"];
var dbName = builder.Configuration["POSTGRES_DB"];
// !!! Важная проверка на null/empty для всех частей строки подключения !!!
if (string.IsNullOrEmpty(dbHost) || string.IsNullOrEmpty(dbPort) || string.IsNullOrEmpty(dbUser) || string.IsNullOrEmpty(dbPassword) || string.IsNullOrEmpty(dbName))
{
    throw new InvalidOperationException("Одна или несколько переменных окружения для подключения" +
                                        " к PostgreSQL не установлены (POSTGRES_HOST, POSTGRES_PORT, POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB).");
}
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT SecretKey ('JwtSettings:SecretKey' или 'JwtSettings__SecretKey') не настроен или пуст.");
}

var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};";

var services = builder.Services;


// 1. Добавление DbContext с использованием строки подключения
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)); 


// 2. Добавление Identity
services.AddIdentity();



// Добавляем аутентификацию
builder.Services.AddAuthentication(options =>
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
            ValidIssuer = jwtSettings["Issuer"], // Добавьте в appsettings.json
            ValidAudience = jwtSettings["Audience"], // Добавьте в appsettings.json
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

var app = builder.Build();


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
app.UseAuthorization();  // Затем проверяем, авторизован ли он для доступа к ресурсу

app.MapControllers();    // Сопоставляем запросы с контроллерами
app.Run();