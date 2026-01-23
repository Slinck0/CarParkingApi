using Microsoft.EntityFrameworkCore;
using V2.Data;
namespace V2.Services;
public static class ServiceHelper
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config)
    {
        // Check Environment specific for Testing
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (env == "Testing" || config["UseInMemoryDatabase"] == "true")
        {
            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase(config["InMemoryDatabaseName"] ?? "TestDb"));
        }
        else
        {
            services.AddDbContext<AppDbContext>(o =>
                o.UseLibSql(config.GetConnectionString("DefaultConnection")));
        }






        services.AddScoped<TokenService>();

        return services;
    }
}
