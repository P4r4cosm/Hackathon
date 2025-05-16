using Microsoft.EntityFrameworkCore;

namespace SoundService.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IConfiguration configuration)
        : base(options)
    {
        Database.EnsureCreated();
    }
}