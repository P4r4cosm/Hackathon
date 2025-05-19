using AuthService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser> // Или IdentityDbContext<IdentityUser>
{

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IConfiguration configuration)
        : base(options)
    {
        // База данных будет создана через MigrateDatabase в Program.cs
    }
}