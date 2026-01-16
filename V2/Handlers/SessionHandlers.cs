using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Helpers;


public static class SessionHandlers
{
    public static async Task<IResult> StartSession(int id, AppDbContext db, HttpContext http, StartStopSessionRequest req)
    {
        var check = await ActiveAccountHelper.CheckActive(http, db);
        if (check != null) return check;

        var parkingLot = await db.ParkingLots.FindAsync(id);
        if (parkingLot == null)
        {
            return Results.NotFound("Parking lot not found.");
        }

        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == req.LicensePlate && v.UserId == userId);
        if (vehicle == null)
        {
            return Results.NotFound("Vehicle not found.");
        }

        var session = new ParkingSessionModel
        {
            UserId = userId,
            VehicleId = vehicle.Id,
            StartTime = DateTime.UtcNow,
            LicensePlate = req.LicensePlate,
            ParkingLotId = parkingLot.Id,
            Status = "active"
        };
        db.ParkingSessions.Add(session);
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = $"Session started for vehicle {req.LicensePlate} at parking lot {parkingLot.Name}."
        });
    }

    public static async Task<IResult> StopSession(int id, AppDbContext db, HttpContext http, StartStopSessionRequest req)
    {
        var check = await ActiveAccountHelper.CheckActive(http, db);
        if (check != null) return check;

        var parkingLot = await db.ParkingLots.FindAsync(id);
        if (parkingLot == null)
        {
            return Results.NotFound("Parking lot not found.");
        }

        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var vehicle = await db.Vehicles
            .FirstOrDefaultAsync(v => v.LicensePlate == req.LicensePlate && v.UserId == userId);
        if (vehicle == null)
        {
            return Results.NotFound("Vehicle not found.");
        }

        var session = await db.ParkingSessions
            .Where(s => s.VehicleId == vehicle.Id && s.EndTime == null)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (session == null)
        {
            return Results.NotFound("Active parking session not found for this vehicle.");
        }

        session.EndTime = DateTime.UtcNow;

        var (price, _, _) = CalculateHelpers.CalculatePrice(parkingLot, session.StartTime, session.EndTime.Value);
        session.Cost = price;
        session.Status = "completed";
        

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = $"Session stopped for vehicle {req.LicensePlate} at parking lot {parkingLot.Name}.",
            cost = session.Cost
        });
    }
}