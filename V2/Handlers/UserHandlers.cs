using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;
using ParkingApi.Services;

public static class UserHandlers
{
    public static IResult Register(RegisterUserRequest req, AppDbContext db)
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
        var user = new User
        {
            Username = req.Username,
            Password = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Email = req.Email,
            Name = req.Name,
            Phone = req.PhoneNumber,
            BirthYear = req.BirthYear,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            Active = true
        };

        db.Users.Add(user);
        db.SaveChanges();
        return Results.Created($"/users/{user.Id}", new { user.Id, user.Username, user.Email });
    }

    public static async Task<IResult> Login(LoginRequest req, AppDbContext db, TokenService token)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        {
            return Results.BadRequest(new
            {
                error = "missing_fields",
                message = "Bad request:/nUsername and Password are required."
            });
        }
        var user = db.Users.FirstOrDefault(u => u.Username == req.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
        {
            return Results.Unauthorized();
        }

        var jwt = token.CreateToken(user);
        return Results.Ok(new
        {
            token = jwt,
            user = new UserResponse(user.Id, user.Username, user.Role.ToString())
        });
    }


    
}