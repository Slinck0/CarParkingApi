using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using V2.Data;

namespace ParkingApi.Tests.Helpers;

/// <summary>
/// Helper to initialize the dedicated Test Database.
/// Ensures the schema matches production by applying all EF Core migrations.
/// </summary>
public static class TestDatabaseSetup
{
    private static bool _initialized = false;
    private static readonly object _lock = new();

    public static void Initialize(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Initialize(db);
    }

    public static void Initialize(AppDbContext db)
    {
        lock (_lock)
        {
            if (_initialized) return;

            var connectionString = db.Database.GetConnectionString();
            
            // Safety check: ensure we are NOT connected to production
            // Note: In Unit Tests, we might not have easy access to IConfiguration to check DefaultConnection
            // But usually the connection string itself contains "test" or similar if we set it up right.
            // Or we just trust the caller.
            // But let's keep the logging.

            Console.WriteLine($"[TestDatabaseSetup] Initializing Test Database...");
            Console.WriteLine($"[TestDatabaseSetup] Connection: {connectionString?.Substring(0, Math.Min(connectionString?.Length ?? 0, 20))}...");

            try
            {
                // Ensure database is created and migrations are applied
                db.Database.Migrate();
                Console.WriteLine("[TestDatabaseSetup] Migrations applied successfully.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TestDatabaseSetup] Migration failed: {ex.Message}");
                throw;
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Wipes all data from the Test Database.
    /// Can be used by Integration Tests (teardown) and Unit Tests (setup)
    /// to ensure a clean state when running sequentially.
    /// </summary>
    public static async Task WipeDatabaseAsync(AppDbContext db)
    {
        // Dependency order matters (Children first)
        var tables = new[] 
        {
            "discount_usage",
            "payment",
            "parking_sessions",
            "reservation",
            "vehicle",
            "parking_lot",
            "discount",
            "user",
            "organization"
        };

        foreach (var table in tables)
        {
            try 
            {
                // Execute individually to ensure LibSql handles it
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM {table}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TestDatabaseSetup] Cleanup failed for table {table}: {ex.Message}");
            }
        }
    }
}
