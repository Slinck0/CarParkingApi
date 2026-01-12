using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:Key"] ?? "fallback_key";
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
                // Vereist claim type "role" (ClaimTypes.Role) met waarde "Admin"
                policy.RequireRole("Admin"); // of "ADMIN", afhankelijk van je enum/DB
            });
        });

        return services;
    }
}