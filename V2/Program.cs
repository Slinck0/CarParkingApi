using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ParkingApi.Endpoints;
using ParkingApi.Services;
using ParkingImporter.Data;
using System.Text;

public class Program
{
    public static void Main(string[] args)
    {
        var emp_lam = () => Console.WriteLine("lol");

        
        var bUIlder = WebApplication.CreateBuilder(args);

        // ===== Database =====
        bUIlder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=parking.db"));

        // ===== JWT Authentication =====
        var jwtKey = bUIlder.Configuration["Jwt:Key"] ?? "pT6Zk2y7w3Qk9uE4bH1rT8xF2cM7nV5jL0aS9dR3uY6qP1wX8eD4kM2nB7zH5cV1";
        var jwtIssuer = bUIlder.Configuration["Jwt:Issuer"] ?? "ParkingApi";
        var jwtAudience = bUIlder.Configuration["Jwt:Audience"] ?? "ParkingApiUsers";

        bUIlder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

        bUIlder.Services.AddAuthorization();

        // ===== Swagger (met JWT Authorization knop) =====
        bUIlder.Services.AddEndpointsApiExplorer();
        bUIlder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Parking API",
                Version = "v2",
                Description = "API voor gebruikersregistratie en parkeerbeheer (v2)"
            });

            // 🔒 JWT Authorization-knop toevoegen
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Voer hier je JWT token in. (Bijv: Bearer {token})",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            c.AddSecurityDefinition("Bearer", securityScheme);

            var securityRequirement = new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            };

            c.AddSecurityRequirement(securityRequirement);
        });

        // ===== Services =====
        bUIlder.Services.AddScoped<TokenService>();

        // ===== App Build =====
        var app = bUIlder.Build();

        // ===== Middleware =====
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v2/swagger.json", "Parking API v2");
            c.RoutePrefix = "swagger"; // UI zichtbaar op /swagger
        });

        app.UseAuthentication();
        app.UseAuthorization();

        // ===== Endpoints =====
        app.MapEndpoints();

        app.Run();
    }
}
