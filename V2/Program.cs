using ParkingApi.Endpoints;
using ParkingApi.Services;
using ParkingApi.Extensions; 

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

        app.Run();
    }
}