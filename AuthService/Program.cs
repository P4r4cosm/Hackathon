
using AuthService.Extensions;
using AuthService.Infrastructure;
using AuthService.Services;
using AuthService.Services.Interfaces;
using DotNetEnv;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;


Env.Load();
var builder = WebApplication.CreateBuilder(args);
var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole()); // Создаем логгер
var programLogger = loggerFactory.CreateLogger<Program>(); // Логгер для Program
builder.Logging.AddConsole();


// Получаем значения из конфигурации для POSTGRES(которая теперь включает .env)
var dbHost = builder.Configuration["POSTGRES_HOST"];
var dbPort = builder.Configuration["POSTGRES_PORT"];
var dbUser = builder.Configuration["POSTGRES_USER"];
var dbPassword = builder.Configuration["POSTGRES_PASSWORD"];
var dbName = builder.Configuration["POSTGRES_DB"];
var requiredEnvVars = new Dictionary<string, string?>
{
    ["POSTGRES_HOST"] = dbHost,
    ["POSTGRES_PORT"] = dbPort,
    ["POSTGRES_USER"] = dbUser,
    ["POSTGRES_PASSWORD"] = dbPassword,
    ["POSTGRES_DB"] = dbName
};

foreach (var (key, value) in requiredEnvVars)
{
    if (string.IsNullOrEmpty(value))
        throw new InvalidOperationException($"Переменная окружения {key} не установлена");
}

// получаем роли
var Roles = builder.Configuration["ROLES"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries);
if (Roles is null || Roles.Count() == 0)
{
    throw new InvalidOperationException("Roles in .env is empty.");
}

//конфигурируем Kestrel (добавляем сертификат)
KestrelConfiguratorHelper.ConfigureKestrel(builder);


var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};";

var services = builder.Services;


// 1. Добавление DbContext с использованием строки подключения
services.AddApplicationDbContext(connectionString);

// 2. Добавление Identity
services.AddIdentity();

// 3. Добавляем DbSeeder
services.AddScoped<DbSeeder>();

// Добавляем аутентификацию

// Добавляем авторизацию

services.AddJwtAuthentication(builder.Configuration);
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("admin"));
    options.AddPolicy("UserPolicy", policy =>
        policy.RequireRole("user"));
});

//TokenService
services.AddScoped<ITokenService, TokenService>();


services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policyBuilder =>
    {
        policyBuilder.WithOrigins("http://localhost:3010 ")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // важно для кук
    });
});



services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // или CookieSecurePolicy.None, но SameAsRequest лучше
    options.Cookie.SameSite = SameSiteMode.Lax; // <--- ИЗМЕНИТЬ НА LAX
    //options.LoginPath = "";
});

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.RequireHeaderSymmetry = false; // Может понадобиться в некоторых сценариях с Docker
});

var app = builder.Build();
app.UseForwardedHeaders();

app.UseCors("AllowFrontend");
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

//app.UseHttpsRedirection();

app.UseRouting(); // <-- Добавляем UseRouting перед Auth


app.UseAuthentication(); // Сначала проверяем, аутентифицирован ли пользователь
app.UseAuthorization(); // Затем проверяем, авторизован ли он для доступа к ресурсу

app.MapControllers(); // Сопоставляем запросы с контроллерами
app.Run();
