using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

public static class ReservationHandlers
{
    public static async Task<IResult> CreateReservation(HttpContext http, ReservationRequest req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.LicensePlate) || !req.StartDate.HasValue || !req.EndDate.HasValue || req.ParkingLot <= 0 || req.VehicleId <= 0)
        {
            return Results.BadRequest(new
            {
                error = "missing_fields",
                message = "Bad request:/nLicensePlate, StartDate, EndDate, ParkingLot and VehicleId are required."
            });
        }

        var startDate = req.StartDate.Value;
        var endDate = req.EndDate.Value;

        if (endDate <= startDate)
            return Results.BadRequest("Bad request:/nEndDate must be after StartDate.");

        var parkingLot = await db.ParkingLots.FirstOrDefaultAsync(p => p.Id == req.ParkingLot);
        if (parkingLot is null)
            return Results.NotFound("Parking lot not found.");
        
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var (price, _, _) = CalculateHelpers.CalculatePrice(parkingLot, startDate, endDate);

        var r = new ReservationModel
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            ParkingLotId = parkingLot.Id,
            VehicleId = req.VehicleId,
            StartTime = startDate,
            EndTime = endDate,
            CreatedAt = DateTime.UtcNow,
            Status = ReservationStatus.confirmed,
            Cost = price
        };

        db.Reservations.Add(r);
        await db.SaveChangesAsync();

        var response = new
        {
            status = "Success",
            reservation = new
            {
                licenseplate = req.LicensePlate,
                startdate = startDate.ToString("yyyy-MM-dd"),
                enddate = endDate.ToString("yyyy-MM-dd"),
                parkinglot = req.ParkingLot.ToString()
            }
        };

        return Results.Created($"/reservations/{r.Id}", response);
    }

    public static async Task<IResult> GetMyReservations(HttpContext http, AppDbContext db)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var reservations = await db.Reservations
            .Where(r => r.UserId == userId)
            .Select(r => new
            {
                r.Id,
                r.ParkingLotId,
                r.VehicleId,
                r.StartTime,
                r.EndTime,
                Status = r.Status.ToString(),
                r.Cost
            })
            .ToListAsync();

        return Results.Ok(reservations);
    }

    public static async Task<IResult> CancelReservation(string id, AppDbContext db)
    {
        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null)
        {
            return Results.NotFound("Reservation not found.");
        }

        if (reservation.Status == ReservationStatus.cancelled)
        {
            return Results.BadRequest("Reservation is already cancelled.");
        }
        
        // Let op: Autorisatie (controleren of dit de reservering van de ingelogde gebruiker is) mist hier! 
        // Je zou de HttpContext moeten doorgeven en ClaimHelper gebruiken.

        reservation.Status = ReservationStatus.cancelled;
        await db.SaveChangesAsync();

        return Results.Ok(new { status = "Success", message = "Reservation cancelled successfully." });
    }
    
    public static async Task<IResult> UpdateReservation(string id, AppDbContext db, ReservationRequest req)
    {
        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null)
        {
            return Results.NotFound("Reservation not found.");
        }
        
        // Let op: Autorisatie (controleren of dit de reservering van de ingelogde gebruiker is) mist hier!

        if (string.IsNullOrWhiteSpace(req.LicensePlate) || !req.StartDate.HasValue || !req.EndDate.HasValue || req.ParkingLot <= 0 || req.VehicleId <= 0)
        {
            return Results.BadRequest(new
            {
                error = "missing_fields",
                message = "Bad request:/nLicensePlate, StartDate, EndDate, ParkingLot and VehicleId are required."
            });
        }
        var startDate = req.StartDate.Value;
        var endDate = req.EndDate.Value;
        if (endDate <= startDate)
        {
            return Results.BadRequest("Bad request:/nEndDate must be after StartDate.");
        }
        var parkingLot = await db.ParkingLots.FirstOrDefaultAsync(p => p.Id == req.ParkingLot);
        if (parkingLot is null)
            return Results.NotFound("Parking lot not found.");

        var (price, _, _) = CalculateHelpers.CalculatePrice(parkingLot, startDate, endDate);

        reservation.ParkingLotId = parkingLot.Id;
        reservation.VehicleId = req.VehicleId;
        reservation.StartTime = startDate;
        reservation.EndTime = endDate;
        reservation.Cost = price;


        await db.SaveChangesAsync();
        return Results.Ok(new
        {
            status = "Success",
            reservation = new
            {
                licenseplate = req.LicensePlate,
                startdate = startDate.ToString("yyyy-MM-dd"),
                enddate = endDate.ToString("yyyy-MM-dd"),
                parkinglot = req.ParkingLot.ToString()
            }
        });
    }
}