using ParkingImporter.Data;
using ParkingApi.Endpoints;
using Microsoft.EntityFrameworkCore;
using Jsonimporter.Api;
using Microsoft.OpenApi.Models; // ✅ Add this

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // ✅ Register DbContext
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=parking.db"));

        var app = builder.Build();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Parking API v1");
        });

        // Map endpoints
        app.MapEndpoints();

        // ✅ Run JSON import AFTER DI is ready
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await AppInt.ImportJson(db);
        }

        app.Run();
    }
}