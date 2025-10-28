using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Import;

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=parking.db")
    .Options;

using var db = new AppDbContext(options);

// Past migraties toe bij runtime (geen EnsureCreated meer!)
db.Database.Migrate();

// Paden naar JSON bestanden
var lotsPath = "parking-lots.json";
var reservationsPath = "reservations.json";
var paymentsPath = "payments.json";

// Import uitvoeren
if (File.Exists(lotsPath))
    await LotsImporter.ImportAsync(db, lotsPath);

if (File.Exists(reservationsPath))
    await ReservationsImporter.ImportAsync(db, reservationsPath);

if (File.Exists(paymentsPath))
    await PaymentsImporter.ImportAsync(db, paymentsPath);

Console.WriteLine("Alle imports gereed ✅");
