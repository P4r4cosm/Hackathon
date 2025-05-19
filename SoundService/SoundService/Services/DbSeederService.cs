using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using SoundService.Data;
using Microsoft.Extensions.Logging;

namespace SoundService.Services;

public class DbSeederService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DbSeederService> _logger;

    public DbSeederService(ApplicationDbContext context, ILogger<DbSeederService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public void Seed()
    {
        try
        {
            _logger.LogInformation("Проверка соединения с базой данных...");
            
            // Проверяем, может ли быть установлено соединение с базой данных
            if (_context.Database.CanConnect())
            {
                _logger.LogInformation("Соединение с базой данных установлено успешно.");
                
                try
                {
                    // Применяем все миграции
                    _logger.LogInformation("Применяю миграции...");
                    _context.Database.Migrate();
                    _logger.LogInformation("Миграции успешно применены.");
                }
                catch (Exception migrateEx)
                {
                    _logger.LogError(migrateEx, "Ошибка при применении миграций. Пытаюсь создать схему базы данных напрямую.");
                    _context.Database.EnsureCreated();
                    _logger.LogInformation("Схема базы данных создана успешно.");
                }
                
                // Проверяем, есть ли таблица AudioRecords
                try {
                    var count = _context.AudioRecords.Count();
                    _logger.LogInformation($"Количество записей в таблице AudioRecords: {count}");
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Таблица AudioRecords не существует. Ошибка: {Message}", ex.Message);
                }
                
                _logger.LogInformation("Инициализация базы данных завершена успешно.");
            }
            else
            {
                _logger.LogError("Невозможно подключиться к базе данных.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Произошла ошибка при инициализации базы данных: {Message}", ex.Message);
        }
    }
}