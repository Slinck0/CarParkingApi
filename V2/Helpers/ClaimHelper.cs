using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Services;

// Hulp methode om de UserId uit de claims te halen
// Deze moet beschikbaar zijn voor alle handlers die claims nodig hebben
public static class ClaimHelper
{
    public static int GetUserId(HttpContext http)
    {
        var userIdClaim = http.User?.Claims
            .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            // In een echte applicatie zou je hier een Unauthorized/Forbidden result retourneren
            // Maar in de handler geven we 0 terug of gooien we een uitzondering als de mapping geen Result kan teruggeven.
            return 0;
        }
        return userId;
    }

    public static int LicensePlateHelper(HttpContext http, string licensePlate, AppDbContext db)
    {
        var userId = GetUserId(http);
        if (userId == 0) return 0;

        var vehicle = db.Vehicles.FirstOrDefault(v => v.LicensePlate == licensePlate && v.UserId == userId);
        if (vehicle == null)
        {
            return 0;
        }
        return vehicle.Id;
    }
}