using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace V2.Services; // Zorg dat dit een namespace heeft

public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:Key"] ?? "fallback_key_die_heel_lang_moet_zijn_voor_security";
        var issuer = config["Jwt:Issuer"] ?? "ParkingApi";
        var audience = config["Jwt:Audience"] ?? "ParkingApiUsers";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                };
            });

        // HIER: policies definiëren
        services.AddAuthorization(options =>
        {
            // Policy met naam "ADMIN"
            options.AddPolicy("ADMIN", policy =>
            {
                // Zorg dat dit exact matcht met de database string ("ADMIN")
                // Dit is de fix voor jouw 403 error!
                policy.RequireRole("ADMIN"); 
            });
        });

        return services;
    }
}