using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using SoundService.Data;

namespace SoundService.Services;

public class DbSeederService
{
    private readonly ApplicationDbContext _context;

    public DbSeederService(ApplicationDbContext context)
    {
        _context = context;
    }
    public void Seed()
    {
        // 1. Проверяем, создана ли БД
        if (_context.Database.IsNpgsql()) // Можно указать конкретный провайдер
        {
            var exists = _context.Database.GetService<IRelationalDatabaseCreator>().Exists();
            if (!exists)
            {
                _context.Database.EnsureCreated(); // Создаём БД и таблицы
            }
        }
    }
}