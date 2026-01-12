using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Helpers;

namespace V2.Helpers;

public static class ActiveAccountHelper
{
    public static async Task<IResult?> CheckActive(HttpContext http, AppDbContext db)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !user.Active)
            return Results.Problem("Account is inactive", statusCode: 403);

        return null; // account is active
    }
}