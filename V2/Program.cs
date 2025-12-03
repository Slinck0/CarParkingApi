using ParkingApi.Endpoints;
using ParkingApi.Services;


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
            if(args.Contains("import"))
            {
                Console.WriteLine("🚀 Starten importeren JSON-data...");
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                AppInt.ImportJson().Wait();
            }
        }

        app.Run();

    }
}
