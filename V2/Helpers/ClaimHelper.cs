using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;
using ParkingApi.Services;

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
}