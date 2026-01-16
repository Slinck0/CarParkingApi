using System.Runtime.CompilerServices;
using V2.Data;
using V2.Models;
public class AdminHelper
{
    public static void AddAdminIfNotExists(AppDbContext db)
    {
        if (!db.Users.Any(u => u.Username == "Rens"))
        {
            db.Users.Add(new UserModel
            {
                Username = "Rens",
                Password = BCrypt.Net.BCrypt.HashPassword("Rens"),
                Name = "Rens Admin",
                Email = "rens@test.nl",
                Phone = "0612345678",
                Role = "ADMIN",
                Active = true,
                CreatedAt = DateOnly.FromDateTime(DateTime.Now),
                BirthYear = 1990
            });
            db.SaveChanges();
        }
    }
    public static void AddParkinglotIfNotExists(AppDbContext db)
    {
        if (!db.ParkingLots.Any(p => p.Name == "Central Park"))
        {
            db.ParkingLots.Add(new ParkingLotModel
            {
                Id = 1600,
                Name = "Test Garage CI",
                Location = "Rotterdam",
                Address = "Stationstraat 1",
                Capacity = 100,
                Tariff = 2.50m,
                Status = "Open"
            });
            db.SaveChanges();
        }
    }
}