using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ParkingImporter.Data;
using ParkingImporter.Import;

namespace Jsonimporter.Api
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
                System.Console.WriteLine("users import gereed ✅");
            }
            if (File.Exists(ResolvePath(vehiclesPath))){
                await VehiclesImporter.ImportAsync(db, ResolvePath(vehiclesPath));
                System.Console.WriteLine("Vehicles import gereed ✅");
            }
            if (File.Exists(ResolvePath(lotsPath))){
                await LotsImporter.ImportAsync(db, ResolvePath(lotsPath));
                System.Console.WriteLine("Parking import gereed ✅");
            }

            if (File.Exists(ResolvePath(reservationsPath))){
                await ReservationsImporter.ImportAsync(db, ResolvePath(reservationsPath));
                System.Console.WriteLine("reservations import gereed ✅");
            }

            if (File.Exists(ResolvePath(paymentsPath))){
                await PaymentsImporter.ImportAsync(db, ResolvePath(paymentsPath));
                System.Console.WriteLine("betalingenimport gereed ✅");
            }

            Console.WriteLine("Alle imports gereed ✅");
        }
    }
}