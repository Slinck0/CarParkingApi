using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

namespace ParkingImporter.Import;

public static class VehiclesImporter
{
    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Bestand niet gevonden: {jsonPath}");

        var json = await File.ReadAllTextAsync(jsonPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var rawList = JsonSerializer.Deserialize<List<VehicleRaw>>(json, opts) ?? new();

        var valid = new List<Vehicle>();
        var bad   = new List<VehicleRaw>();

        foreach (var r in rawList)
        {
            if (!int.TryParse(r.id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            { bad.Add(r); continue; }

            if (!int.TryParse(r.user_id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
            { bad.Add(r); continue; }

            int year = 0;
            if (!string.IsNullOrWhiteSpace(r.year))
                int.TryParse(r.year, NumberStyles.Integer, CultureInfo.InvariantCulture, out year);

            DateOnly created = default;
            if (!string.IsNullOrWhiteSpace(r.created_at))
            {
                if (!DateOnly.TryParse(r.created_at, CultureInfo.InvariantCulture, DateTimeStyles.None, out created) &&
                    !DateOnly.TryParseExact(r.created_at, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out created))
                {
                    bad.Add(r);
                    continue;
                }
            }

            valid.Add(new Vehicle
            {
                Id           = id,
                UserId       = userId,
                LicensePlate = r.license_plate,
                Make         = r.make,
                Model        = r.model,
                Color        = r.color,
                Year         = year,
                CreatedAt    = created
            });
        }

        var ids = valid.Select(v => v.Id).ToList();
        var existing = await db.Vehicles.Where(v => ids.Contains(v.Id))
                                        .ToDictionaryAsync(v => v.Id);

        foreach (var v in valid)
        {
            if (existing.TryGetValue(v.Id, out var ex))
                db.Entry(ex).CurrentValues.SetValues(v);
            else
                await db.Vehicles.AddAsync(v);
        }

        await db.SaveChangesAsync();

        if (bad.Count > 0)
        {
            await File.WriteAllTextAsync("bad-vehicles.json",
                JsonSerializer.Serialize(bad, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Vehicles import: ok={valid.Count}, overgeslagen={bad.Count} → bad-vehicles.json");
        }
    }
}
