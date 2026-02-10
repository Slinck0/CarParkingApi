using Microsoft.EntityFrameworkCore;
using V2.Data;
namespace V2.Services;
public static class ServiceHelper
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration config)
    {
        // Check if we should use real database (for integration tests)
        var useRealDatabase = config["USE_REAL_DATABASE"] == "true" || Environment.GetEnvironmentVariable("USE_REAL_DATABASE") == "true";
        
        // Check Environment specific for Testing
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        
        if (useRealDatabase)
        {
            // Integration tests: Use real Turso database
            services.AddDbContext<AppDbContext>(o =>
            {
                var connStr = config.GetConnectionString("TestConnection") ?? config.GetConnectionString("DefaultConnection");
                o.UseLibSql(connStr);
            });
        }
        else if (env == "Testing" || config["UseInMemoryDatabase"] == "true")
        {
            // Unit tests: Use Test Database (Real DB) per user request
            // Was InMemory, now switching to Dedicated Test DB
            services.AddDbContext<AppDbContext>(o =>
            {
                var connStr = config.GetConnectionString("TestConnection") ?? config.GetConnectionString("DefaultConnection");
                o.UseLibSql(connStr);
            });
        }
        else
        {
            // Production: Use real Turso database
            services.AddDbContext<AppDbContext>(o =>
                o.UseLibSql(config.GetConnectionString("DefaultConnection")));
        }






        services.AddScoped<TokenService>();

        return services;
    }
}
