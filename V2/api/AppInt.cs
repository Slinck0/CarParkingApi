namespace Jsonimporter.Api;
 
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Import;
public static class AppInt
{
    public static async Task ImportJson()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite("Data Source=parking.db").Options;
 
        using var db = new AppDbContext(options);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
 
 
 
        var lotsPath = "parking-lots.json";
        var reservationsPath = "reservations.json";
        var usersPath = "users.json";
        var vehiclesPath = "vehicles.json";
        var paymentsPath = "payments.json";
 
        if (File.Exists(usersPath))
            await UsersImporter.ImportAsync(db, usersPath);
 
        if (File.Exists(vehiclesPath))
            await VehiclesImporter.ImportAsync(db, vehiclesPath);
 
        if (File.Exists(lotsPath))
            await LotsImporter.ImportAsync(db, lotsPath);
 
        if (File.Exists(reservationsPath))
            await ReservationsImporter.ImportAsync(db, reservationsPath);
 
        if (File.Exists(paymentsPath))
            await PaymentsImporter.ImportAsync(db, paymentsPath);
 
 
        Console.WriteLine("Alle imports gereed ✅");
 
    }
}