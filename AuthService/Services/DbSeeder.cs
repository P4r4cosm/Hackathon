using AuthService.Models;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Services;

public class DbSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;

    public DbSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    public async Task SeedAsync(string[] rolesToCreate)
    {
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