
using System.Text;
using DotNetEnv;
using Microsoft.OpenApi.Models;
using SoundService.Abstractions;
using SoundService.Extensions;
using SoundService.Models.Settings;
using SoundService.RabbitMQ;
using SoundService.Repositories;
using SoundService.Services;

Env.Load();
Console.OutputEncoding = Encoding.UTF8;
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

//minio
var envminioDict = new Dictionary<string, string?>
{
    ["MINIO_ENDPOINT"] = builder.Configuration["MINIO_ENDPOINT"],
    ["MINIO_PORT"] = builder.Configuration["MINIO_PORT"],
    ["MINIO_ACCESS_KEY"] = builder.Configuration["MINIO_ACCESS_KEY"],
    ["MINIO_SECRET_KEY"] = builder.Configuration["MINIO_SECRET_KEY"],
    ["MINIO_BUCKET_NAME"] = builder.Configuration["MINIO_BUCKET_NAME"]
};
foreach (var (key, value) in envminioDict)
{
    if (string.IsNullOrEmpty(value))
        throw new InvalidOperationException($"Переменная окружения {key} не установлена");
}

var minioSettings = new MinioSettings()
{
    Endpoint = envminioDict["MINIO_ENDPOINT"],
    Port=int.Parse(envminioDict["MINIO_PORT"]),
    AccessKey = envminioDict["MINIO_ACCESS_KEY"],
    SecretKey = envminioDict["MINIO_SECRET_KEY"],
    BucketName = envminioDict["MINIO_BUCKET_NAME"]
};



var services = builder.Services;
//подключаем сервисы
services.AddApplicationDbContext(connectionString);
services.AddControllers();
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("sound-v1", new OpenApiInfo { Title = "SoundService", Version = "v1" });
});
services.AddScoped<AudioMetadataService>();
services.AddOpenApi();
services.AddScoped<DbSeederService>();
services.AddMinIO(minioSettings);
services.AddElastic(elasticUri);
//репозитории
services.AddScoped<AudioRecordRepository>();
services.AddScoped<AuthorRepository>();
services.AddScoped<GenreRepository>();


//rabbit
builder.Services.AddSingleton<RabbitMqConf>();
builder.Services.AddHostedService<RabbitMQInitializer>();
builder.Services.AddSingleton<RabbitMqService>();

builder.Services.AddScoped<ITaskResultHandler, TaskResultHandler>();
builder.Services.AddHostedService<RabbitMqResultListener>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeederService>();
    seeder.Seed();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/sound-v1/swagger.json", "SoundService v1");
    options.RoutePrefix = ""; // Доступ по /
});
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.MapControllers();

app.Run();