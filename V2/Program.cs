using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using ParkingImporter.Data;
using ParkingApi.Endpoints;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ===== Database =====
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=parking.db"));

        // ===== Swagger/OpenAPI (v2) =====
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Parking API",
                Version = "v2",
                Description = "API voor gebruikersregistratie en parkeerbeheer (v2)"
            });
        });

        var app = builder.Build();


        app.UseSwagger(); 

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v2/swagger.json", "Parking API v2"); 
            c.RoutePrefix = "swagger"; 
        });

        // ===== Jouw endpoints =====
        app.MapEndpoints();

        app.Run();
    }
}
