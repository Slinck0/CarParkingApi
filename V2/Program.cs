using V2.Endpoints;
using V2.Services;
using V2.Data;
using V2.Api;
using Microsoft.Extensions.DependencyInjection;

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

        app.Run();
    }
}
