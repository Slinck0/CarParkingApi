using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using V2.Data;
using System.Linq;
using ParkingApi.Tests.Helpers;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures the application to use the REAL Turso database for accurate integration testing.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("USE_REAL_DATABASE", "true");
    }


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            // Load the Turso connection string from V2/appsettings.json
            // Navigate from test bin directory up to solution root, then into V2
            var testAssemblyPath = typeof(CustomWebApplicationFactory).Assembly.Location;
            var testBinDir = Path.GetDirectoryName(testAssemblyPath)!;
            
            // Go up from bin/Debug/net9.0 to ParkingApi.Tests, then up to solution root
            var solutionRoot = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", ".."));
            var v2AppsettingsPath = Path.Combine(solutionRoot, "V2", "appsettings.json");
            
            // Load V2/appsettings.json if it exists (local dev).
            // In CI the file may not exist — config comes from environment variables instead.
            configBuilder.AddJsonFile(
                v2AppsettingsPath,
                optional: true,
                reloadOnChange: false
            );

            // Environment variables override appsettings.json (used in GitHub Actions CI)
            // e.g. ConnectionStrings__TestConnection overrides ConnectionStrings:TestConnection
            configBuilder.AddEnvironmentVariables();

            // CRITICAL: Set flag to force real database usage for integration tests
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["USE_REAL_DATABASE"] = "true"
            });
        });

        builder.ConfigureLogging((context, logging) =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
        });
    }



    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var host = base.CreateHost(builder);
        
        // Initialize Test Database Schema (Migrations)
        TestDatabaseSetup.Initialize(host.Services);
        
        return host;
    }
}
