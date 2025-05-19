using AuthService.Extensions;
using AuthService.Infrastructure;
using AuthService.Services;
using DotNetEnv;
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
services.AddJwtAuthentication(builder.Configuration);
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

//app.UseHttpsRedirection();

app.UseRouting(); // <-- Добавляем UseRouting перед Auth


app.UseAuthentication(); // Сначала проверяем, аутентифицирован ли пользователь
app.UseAuthorization(); // Затем проверяем, авторизован ли он для доступа к ресурсу

// Применяем миграции базы данных перед сопоставлением контроллеров
app.MigrateDatabase();

app.MapControllers(); // Сопоставляем запросы с контроллерами
app.Run();
