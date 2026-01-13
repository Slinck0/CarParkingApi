using V2.Endpoints;
using V2.Services;
using V2.Data;
using V2.Api;
using V2.Models; // Zorg dat je de namespace van UserModel hebt

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAppServices(builder.Configuration); 
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddSwaggerDocumentation();

        var app = builder.Build();

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
                    Role = "Admin",
                    Active = true,
                    CreatedAt = DateOnly.FromDateTime(DateTime.Now),
                    BirthYear = 1990
                });
                db.SaveChanges();
            }

            // 3. Je bestaande import logica
            if(args.Contains("import"))
            {
                Console.WriteLine("Starten importeren JSON-data...");
                AppInt.ImportJson().Wait();
            }
        }

        app.Run();
    }
}