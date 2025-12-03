using Microsoft.EntityFrameworkCore;
using V2.Data;
namespace V2.Services;
public static class ServiceHelper
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite("Data Source=parking.db"));

        services.AddScoped<TokenService>();

        return services;
    }
}
