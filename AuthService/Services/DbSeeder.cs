using AuthService.Data;
using AuthService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class DbSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public DbSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration, ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context;
    }

    public void Seed()
    {
        try
        {
            // Сначала проверяем, можем ли подключиться к базе данных
            if (_context.Database.CanConnect())
            {
                // Если таблицы не существуют, создаем их
                if (!_context.Database.GetPendingMigrations().Any())
                {
                    // База данных существует и миграции применены
                    Console.WriteLine("База данных уже существует и все миграции применены.");
                    return;
                }
            }

            // Если не можем подключиться или есть ожидающие миграции
            Console.WriteLine("Применяем миграции к базе данных...");
            _context.Database.Migrate();
            Console.WriteLine("Миграции успешно применены.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при миграции базы данных: {ex.Message}");
            
            // Последняя попытка - создание схемы через EnsureCreated
            try
            {
                Console.WriteLine("Попытка создать схему через EnsureCreated...");
                _context.Database.EnsureCreated();
                Console.WriteLine("Схема базы данных успешно создана.");
            }
            catch (Exception createEx)
            {
                Console.WriteLine($"Не удалось создать схему: {createEx.Message}");
                throw; // Пробрасываем исключение дальше
            }
        }
    }

    public async Task SeedAsync(string[] rolesToCreate)
    {
        Seed();
        foreach (var roleName in rolesToCreate)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        var adminName = _configuration["Admin_name"];
        var adminEmail = _configuration["Admin_email"];
        var adminPassword = _configuration["Admin_password"];
        var adminRole = _configuration["Admin_role"];


        // Проверяем и создаём роль, если не существует
        var roleExists = await _roleManager.RoleExistsAsync(adminRole);
        if (!roleExists)
        {
            await _roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        // Проверяем и создаём пользователя, если не существует
        var user = await _userManager.FindByEmailAsync(adminEmail);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = adminName,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, adminPassword);
            if (!result.Succeeded)
                throw new Exception($"Failed to create admin user: {string.Join(", ", result.Errors)}");

            // Назначаем роль администратору
            await _userManager.AddToRoleAsync(user, adminRole);
        }
    }
}