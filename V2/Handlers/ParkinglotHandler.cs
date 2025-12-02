using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;
public static class ParkingLotHandlers
{
    public static async Task<IResult> CreateParkingLot(ParkingLotCreate req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Capacity <= 0 || string.IsNullOrWhiteSpace(req.Location) || string.IsNullOrWhiteSpace(req.Address) || req.Tariff <= 0 || req.DayTariff == null || req.Lat == 0 || req.Lng == 0)
        {
            return Results.BadRequest("Invalid parking lot data.");
        }

        var parkingLot = new ParkingLot
        {
            Name = req.Name,
            Capacity = req.Capacity,
            Location = req.Location,
            Address = req.Address,
            Tariff = req.Tariff,
            DayTariff = req.DayTariff,
            Lat = req.Lat,
            Lng = req.Lng,
            Reserved = 0,
            Status = req.Status,
            ClosedReason = req.ClosedReason,
            ClosedDate = req.ClosedDate,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        db.ParkingLots.Add(parkingLot);
        await db.SaveChangesAsync();

        return Results.Created($"/parking-lots/{parkingLot.Id}", new
        {
            message = "Parking lot created successfully.",
            parkingLotId = parkingLot.Id,
            parkingLotName = parkingLot.Name,
            parkingLotAddress = parkingLot.Address
        });
    }
}