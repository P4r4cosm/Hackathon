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
        _context.Database.Migrate();
    }
}