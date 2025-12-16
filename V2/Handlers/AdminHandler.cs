using ParkingImporter.Data;

public static class AdminHandler
{
    public static async Task<IResult> CreateAdminUser(HttpContext http, AppDbContext db, CreateAdminRequest req, int userId)
    {
        if (userId <= 0)
            return Results.BadRequest("Invalid User ID.");
        var user = await db.Users.FindAsync(userId);
        if (user == null)
            return Results.NotFound("User not found.");
        user.Role = "ADMIN";
        await db.SaveChangesAsync();
        return Results.Ok(user);
    }
    
    public static async Task<IResult> RemoveAdminUser(HttpContext http, AppDbContext db, int userId)
    {
        if (userId <= 0)
            return Results.BadRequest("Invalid User ID.");
        var user = await db.Users.FindAsync(userId);
        if (user == null)
            return Results.NotFound("User not found.");
        user.Role = "USER";
        await db.SaveChangesAsync();
        return Results.Ok(user);
    }
}