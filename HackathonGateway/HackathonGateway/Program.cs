var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
services.AddOpenApi();
services.AddAuthentication();
services.AddAuthorization();
services.AddCors();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Эндпоинт для получения OpenAPI JSON

    // Добавляем Swagger UI
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
        options.RoutePrefix = ""; // URL: https://localhost:xxxx/swagger
    });
}
app.UseHttpsRedirection();

app.Run();
