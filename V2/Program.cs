using V2.Endpoints;
using V2.Services;
using V2.Data;
using V2.Api;
using Microsoft.Extensions.DependencyInjection;
using V2.Models;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAppServices(builder.Configuration);
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerDocumentation();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        }

        app.UseSwaggerDocumentation();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEndpoints();

        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<AppDbContext>();


            db.Database.EnsureCreated();


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
            if (!db.ParkingLots.Any(p => p.Id == 1))
            {
                db.ParkingLots.Add(new ParkingLotModel
                {
                    Id = 1,
                    Name = "Test Garage CI",
                    Location = "Rotterdam",
                    Address = "Stationstraat 1",
                    Capacity = 100,
                    Tariff = 2.50m,
                    Status = "Open"
                });
            }

            if (args.Contains("import"))
            {
                Console.WriteLine("Starten importeren JSON-data...");
                AppInt.ImportJson().Wait();
            }
        }

        app.Run();
    }
}