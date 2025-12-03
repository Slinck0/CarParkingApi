using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

// Deze Helper-methode (ClaimHelper.GetUserId) is afkomstig uit de vorige refactoring
// en is nodig voor alle geautoriseerde handlers.
public static class ProfileHandlers
{
    // GET /profile
    public static async Task<IResult> GetProfile(HttpContext http, AppDbContext db)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return Results.NotFound("User not found.");

        // DTO voor de output (vermijd het retourneren van het wachtwoordhash)
        var profile = new
        {
            user.Id,
            user.Username,
            user.Name,
            user.Email,
            user.Phone,
            user.Role,
            user.BirthYear,
            user.CreatedAt,
            user.Active
        };

        return Results.Ok(profile);
    }

    // PUT /profile
    public static async Task<IResult> UpdateProfile(HttpContext http, AppDbContext db, UpdateProfileRequest req)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        // 1. Validatie van inkomende data
        if (string.IsNullOrWhiteSpace(req.Name) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.PhoneNumber) ||
            req.BirthYear <= 0)
        {
            return Results.BadRequest("Name, Email, PhoneNumber and BirthYear are required.");
        }

        if (!req.Email.Contains("@") || !req.Email.Contains("."))
        {
            return Results.BadRequest("Invalid email format.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return Results.NotFound("User not found.");

        // 2. Controleer of het nieuwe e-mailadres niet al door een andere gebruiker in gebruik is
        var emailExists = await db.Users
            .AnyAsync(u => u.Email == req.Email && u.Id != user.Id);

        if (emailExists)
        {
            return Results.Conflict("Email is already in use by another account.");
        }

        // 3. Update de gebruikerseigenschappen
        user.Name = req.Name;
        user.Email = req.Email;
        user.Phone = req.PhoneNumber;
        user.BirthYear = req.BirthYear;

        await db.SaveChangesAsync();

        // DTO voor de output
        var profile = new
        {
            user.Id,
            user.Username,
            user.Name,
            user.Email,
            user.Phone,
            user.Role,
            user.BirthYear,
            user.CreatedAt,
            user.Active
        };

        return Results.Ok(profile);
    }

    // DELETE /profile
    public static async Task<IResult> DeleteProfile(HttpContext http, AppDbContext db)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();
        
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return Results.NotFound("User not found.");

        // Verwijder het gebruikersaccount
        db.Users.Remove(user);
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            status = "Success",
            message = "User account deleted successfully."
        });
    }
}