using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

namespace ParkingImporter.Import;

public static class UsersImporter
{
    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        var json = await File.ReadAllTextAsync(jsonPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        
        var rawList = JsonSerializer.Deserialize<List<UserRaw>>(json, opts) ?? new();

        var valid = new List<User>();
        var bad   = new List<UserRaw>();

        foreach (var r in rawList)
        {
            if (!int.TryParse(r.id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            { bad.Add(r); continue; }

            // probeer created_at als DateOnly (pas formaat aan indien nodig)
            DateOnly created = default;
            if (!string.IsNullOrWhiteSpace(r.created_at))
            {
                if (!DateOnly.TryParse(r.created_at, CultureInfo.InvariantCulture, DateTimeStyles.None, out created) &&
                    !DateOnly.TryParseExact(r.created_at, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out created))
                { bad.Add(r); continue; }
            }

            int birthYear = 0;
            if (!string.IsNullOrWhiteSpace(r.birth_year))
                int.TryParse(r.birth_year, NumberStyles.Integer, CultureInfo.InvariantCulture, out birthYear);

            bool active = false;
            if (!string.IsNullOrWhiteSpace(r.active))
                active = r.active.Equals("true", StringComparison.OrdinalIgnoreCase) || r.active == "1";

            valid.Add(new User
            {
                Id        = id,
                Username  = r.username,
                Password  = r.password,
                Name      = r.name,
                Email     = r.email,
                Phone     = r.phone,
                Role      = string.IsNullOrWhiteSpace(r.role) ? "USER" : r.role!,
                CreatedAt = created,
                BirthYear = birthYear,
                Active    = active
            });
        }

        var ids = valid.Select(u => u.Id).ToList();
        var existing = await db.Users.Where(u => ids.Contains(u.Id))
                                     .ToDictionaryAsync(u => u.Id);

        foreach (var u in valid)
        {
            if (existing.TryGetValue(u.Id, out var ex))
                db.Entry(ex).CurrentValues.SetValues(u);
            else
                await db.Users.AddAsync(u);
        }

        await db.SaveChangesAsync();

        if (bad.Count > 0)
        {
            await File.WriteAllTextAsync("bad-users.json",
                JsonSerializer.Serialize(bad, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Users import: ok={valid.Count}, overgeslagen={bad.Count} → bad-users.json");
        }
    }
}
