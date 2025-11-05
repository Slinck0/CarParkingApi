using System.Data.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ParkingImporter.Data;


namespace ParkingApi.Endpoints;
public static class Endpoints
{
    public static void MapEndpoints( this WebApplication app)
    {
        app.MapGet("/Health", () => "Parking API is running...");

        app.MapPost("/register", (RegisterUserRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.PhoneNumber) || req.BirthYear <= 0)
            {
                return Results.BadRequest("Bad request:/nUsername, Password, Name, PhoneNumber, Email and BirthYear are required.");
            }
            if (!req.Email.Contains("@") || !req.Email.Contains("."))
            {
                return Results.BadRequest("Bad request:/nInvalid email format.");
            }
  
            var exist = db.Users.Any(u => u.Username == req.Username || u.Email == req.Email);
            if (exist)
            {   
                return Results.Conflict("Bad request:/nUsername or Email already exists.");
            }
            var user = new ParkingImporter.Models.User
            {
                Username = req.Username,
                Password = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Email = req.Email,
                Name = req.Name,
                Phone = req.PhoneNumber,
                BirthYear = req.BirthYear,
                CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            db.Users.Add(user);
            db.SaveChanges();
            return Results.Created($"/users/{user.Id}", new { user.Id, user.Username, user.Email });
        });
        
        app.MapPost("/login", (LoginRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest("Bad request:/nUsername and Password are required.");
            }

            
            return Results.Ok("Login endpoint - logic not implemented yet.");
        });
    }
}