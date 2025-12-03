using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Import;

namespace V2.Api
{
    public static class AppInt
    {
        public static async Task ImportJson()
        {
            // Load configuration
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using var db = new AppDbContext(options);
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");

            // Get JSON file paths from configuration
            var paths = config.GetSection("JsonPaths");
            var lotsPath = paths["Lots"];
            var reservationsPath = paths["Reservations"];
            var usersPath = paths["Users"];
            var vehiclesPath = paths["Vehicles"];
            var paymentsPath = paths["Payments"];

            // Make sure paths are relative to project root, not bin/
            string rootDir = Path.Combine(Directory.GetCurrentDirectory());
            string ResolvePath(string relativePath) => Path.GetFullPath(Path.Combine(rootDir, relativePath));

            if (File.Exists(ResolvePath(usersPath)))
            {
                await UsersImporter.ImportAsync(db, ResolvePath(usersPath));
                Console.WriteLine("users import gereed ✅");
            }
            if (File.Exists(ResolvePath(vehiclesPath))){
                await VehiclesImporter.ImportAsync(db, ResolvePath(vehiclesPath));
                Console.WriteLine("Vehicles import gereed ✅");
            }
            if (File.Exists(ResolvePath(lotsPath))){
                await ParkingLotsImporter.ImportAsync(db, ResolvePath(lotsPath));
                Console.WriteLine("Parking import gereed ✅");
            }

            if (File.Exists(ResolvePath(reservationsPath))){
                await ReservationsImporter.ImportAsync(db, ResolvePath(reservationsPath));
                Console.WriteLine("reservations import gereed ✅");
            }

            if (File.Exists(ResolvePath(paymentsPath))){
                await PaymentsImporter.ImportAsync(db, ResolvePath(paymentsPath));
                Console.WriteLine("betalingenimport gereed ✅");
            }

            Console.WriteLine("Alle imports gereed ✅");
        }
    }
}