
using DotNetEnv;
using SoundService.Extensions;
using SoundService.Repositories;
using SoundService.Services;

Env.Load();
var builder = WebApplication.CreateBuilder(args);

// Увеличиваем лимит на чтение тела запроса (например, до 200 МБ)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200_000_000; // 200 MB
});
builder.Logging.AddConsole();
// Add services to the container.
//postgres
var dbHost = builder.Configuration["POSTGRES_HOST"];
var dbPort = builder.Configuration["POSTGRES_PORT"];
var dbUser = builder.Configuration["POSTGRES_USER"];
var dbPassword = builder.Configuration["POSTGRES_PASSWORD"];
var dbName = builder.Configuration["POSTGRES_DB"];
var envPostgresDict = new Dictionary<string, string?>
{
    ["POSTGRES_HOST"] = dbHost,
    ["POSTGRES_PORT"] = dbPort,
    ["POSTGRES_USER"] = dbUser,
    ["POSTGRES_PASSWORD"] = dbPassword,
    ["POSTGRES_DB"] = dbName
};

foreach (var (key, value) in envPostgresDict)
{
    if (string.IsNullOrEmpty(value))
        throw new InvalidOperationException($"Переменная окружения {key} не установлена");
}
var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};";

//elastic
var elasticHost = builder.Configuration["ELASTIC_URI"];
if (string.IsNullOrEmpty(elasticHost))
    throw new InvalidOperationException($"Переменная ELASTIC_URI не установлена");

var elasticUri = new Uri(elasticHost);

var services = builder.Services;
//подключаем DBcontext
services.AddApplicationDbContext(connectionString);
services.AddControllers();
services.AddSwaggerGen();
services.AddScoped<AudioMetadataService>();
services.AddOpenApi();
services.AddScoped<DbSeederService>();
services.AddElastic(elasticUri);
//репозитории
services.AddScoped<AudioRecordRepository>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeederService>();
    seeder.Seed();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SoundService v1");
    options.RoutePrefix = ""; // Доступ по /
});
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.MapControllers();

app.Run();