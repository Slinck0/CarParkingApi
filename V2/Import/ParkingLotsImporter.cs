using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

namespace V2.Import;
using System.Diagnostics.CodeAnalysis;
[ExcludeFromCodeCoverage]
public static class ParkingLotsImporter
{
    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        var json = await File.ReadAllTextAsync(jsonPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = JsonSerializer.Deserialize<Dictionary<string, ParkingLotsRaw>>(json, opts) ?? new();

        var valid = new List<ParkingLotModel>();
        var bad   = new List<ParkingLotsRaw>();
        const string df = "yyyy-MM-dd";

        foreach (var r in root.Values)
        {
            if (!int.TryParse(r.id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            { bad.Add(r); continue; }

            if (!DateOnly.TryParseExact(r.created_at, df, CultureInfo.InvariantCulture, DateTimeStyles.None, out var created))
            { bad.Add(r); continue; }

            var closedDate = string.IsNullOrWhiteSpace(r.closed_date) ? (DateOnly?)null
                : (DateOnly.TryParseExact(r.closed_date, df, CultureInfo.InvariantCulture, DateTimeStyles.None, out var cd) ? cd : null);

            valid.Add(new ParkingLotModel {
                Id = id,
                Name = r.name,
                Location = r.location,
                Address = r.address,
                Capacity = r.capacity,
                Reserved = r.reserved,
                Tariff = r.tariff,
                DayTariff = r.daytariff,
                CreatedAt = created,
                Lat = r.coordinates?.lat ?? 0,
                Lng = r.coordinates?.lng ?? 0,
                Status = r.status,
                ClosedReason = r.closed_reason,
                ClosedDate = closedDate
            });
        }

        var ids = valid.Select(v => v.Id).ToList();
        var existing = await db.ParkingLots.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        foreach (var e in valid)
        {
            if (existing.TryGetValue(e.Id, out var t))
            {
                t.Name = e.Name; t.Location = e.Location; t.Address = e.Address;
                t.Capacity = e.Capacity; t.Reserved = e.Reserved;
                t.Tariff = e.Tariff; t.DayTariff = e.DayTariff; t.CreatedAt = e.CreatedAt;
                t.Lat = e.Lat; t.Lng = e.Lng; t.Status = e.Status;
                t.ClosedReason = e.ClosedReason; t.ClosedDate = e.ClosedDate;
            }
            else
            {
                await db.ParkingLots.AddAsync(e);
            }
        }

        await db.SaveChangesAsync();

        if (bad.Count > 0)
            await File.WriteAllTextAsync("bad-parking-lots.json", JsonSerializer.Serialize(bad, new JsonSerializerOptions{WriteIndented = true}));
    }
}
