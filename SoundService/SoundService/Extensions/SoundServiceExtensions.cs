using Microsoft.EntityFrameworkCore;
using SoundService.Data;

namespace SoundService.Extensions;

public static class SoundServiceExtensions
{
    public static IServiceCollection AddApplicationDbContext(this IServiceCollection services, string connectionString)
    {
        return services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
    }
}