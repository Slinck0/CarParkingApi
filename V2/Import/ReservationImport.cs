using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

namespace V2.Import;

public static class ReservationsImporter
{
    public static async Task ImportAsync(AppDbContext db, string jsonPath)
    {
        var json = await File.ReadAllTextAsync(jsonPath);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var raw  = JsonSerializer.Deserialize<List<ReservationRaw>>(json, opts) ?? new();

        var valid = new List<Reservation>();
        var bad   = new List<ReservationRaw>();

        int n = 0;
        foreach (var r in raw)
        {
            n++;
            if (string.IsNullOrWhiteSpace(r.id)) { bad.Add(r); continue; }

            if (!int.TryParse(r.user_id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId) ||
                !int.TryParse(r.parking_lot_id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lotId) ||
                !int.TryParse(r.vehicle_id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vehicleId))
            { bad.Add(r); continue; }

            if (!DateTimeOffset.TryParse(r.start_time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start) ||
                !DateTimeOffset.TryParse(r.end_time,   CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end) ||
                !DateTimeOffset.TryParse(r.created_at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created))
            { bad.Add(r); continue; }

            var status = Enum.TryParse<ReservationStatus>(r.status, true, out var st) ? st : ReservationStatus.pending;

            valid.Add(new Reservation {
                Id = r.id,
                UserId = userId,
                ParkingLotId = lotId,
                VehicleId = vehicleId,
                StartTime = start,
                EndTime = end,
                CreatedAt = created,
                Status = status,
                Cost = r.cost
            });
        }

        var keys = valid.Select(v => v.Id).ToList();
        var existing = await db.Reservations.Where(x => keys.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        foreach (var e in valid)
        {
            if (existing.TryGetValue(e.Id, out var t))
            {
                t.UserId = e.UserId; t.ParkingLotId = e.ParkingLotId; t.VehicleId = e.VehicleId;
                t.StartTime = e.StartTime; t.EndTime = e.EndTime; t.CreatedAt = e.CreatedAt;
                t.Status = e.Status; t.Cost = e.Cost;
            }
            else
            {
                await db.Reservations.AddAsync(e);
            }
        }

        await db.SaveChangesAsync();

        if (bad.Count > 0)
            await File.WriteAllTextAsync("bad-reservations.json", JsonSerializer.Serialize(bad, new JsonSerializerOptions{WriteIndented = true}));
    }
}
