using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures the application to use an in-memory database via configuration.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;

    public CustomWebApplicationFactory()
    {
        _dbName = $"TestDb_{Guid.NewGuid()}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Add test configuration that tells the app to use InMemory database
        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["InMemoryDatabaseName"] = _dbName
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        return base.CreateHost(builder);
    }
}
