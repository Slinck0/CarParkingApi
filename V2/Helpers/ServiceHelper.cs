using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
namespace ParkingApi.Services;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite("Data Source=parking.db"));

        services.AddScoped<TokenService>();

        return services;
    }
}
