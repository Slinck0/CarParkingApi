using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using V2.Data;
using V2.Models;
using V2.Helpers;
using V2.Services;


public static class ReservationHandlers
{
    public static async Task<IResult> CreateReservation(HttpContext http, ReservationRequest req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.LicensePlate) || !req.StartDate.HasValue || !req.EndDate.HasValue || req.ParkingLot <= 0 )
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

        // Check for conflicts
        var vehicleIdCheck = ClaimHelper.LicensePlateHelper(http, req.LicensePlate, db);
        if (vehicleIdCheck != 0)
        {
            // Fetch potential conflicting reservations for this vehicle
            // Optimization: Filter by VehicleId only in DB, then filter status and dates in memory to avoid LINQ translation issues
            var existingReservations = await db.Reservations
                .Where(r => r.VehicleId == vehicleIdCheck)
                .ToListAsync();

            bool hasConflict = existingReservations.Any(r => 
                r.Status != ReservationStatus.cancelled &&
                ((startDate >= r.StartTime && startDate < r.EndTime) || 
                 (endDate > r.StartTime && endDate <= r.EndTime) ||
                 (startDate <= r.StartTime && endDate >= r.EndTime)));

            if (hasConflict)
            {
                return Results.Conflict("A reservation for this vehicle already exists during the specified time.");
            }
        }

        // Check authorization first before any database lookups
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var parkingLot = await db.ParkingLots.FirstOrDefaultAsync(p => p.Id == req.ParkingLot);
        if (parkingLot is null)
            return Results.NotFound("Parking lot not found.");

        var vehicleId = ClaimHelper.LicensePlateHelper(http, req.LicensePlate, db);
        if (vehicleId == 0)
        {
            return Results.BadRequest(new
            {
                error = "vehicle_not_found",
                message = "Vehicle with provided license plate not found for this user."
            });
        }

        // NEW: Validate and apply discount code if provided
        DiscountModel? discount = null;
        if (!string.IsNullOrWhiteSpace(req.DiscountCode))
        {
            var (isValid, message, validDiscount) =
                await DiscountValidationService.ValidateDiscountCodeAsync(
                    req.DiscountCode,
                    userId,
                    parkingLot.Id,
                    startDate,
                    endDate,
                    db);

            if (!isValid)
            {
                return Results.BadRequest(new
                {
                    error = "invalid_discount",
                    message
                });
            }

            discount = validDiscount;
        }

        // Calculate price with discount
        var (originalPrice, discountAmount, finalPrice) =
            CalculateHelpers.CalculatePriceWithDiscount(
                parkingLot,
                startDate,
                endDate,
                discount);

        var reservationId = Guid.NewGuid().ToString();

        // Create reservation with discount information
        var r = new ReservationModel
        {
            Id = reservationId,
            UserId = userId,
            ParkingLotId = parkingLot.Id,
            VehicleId = vehicleId,
            StartTime = startDate,
            EndTime = endDate,
            CreatedAt = DateTime.UtcNow,
            Status = ReservationStatus.confirmed,
            OriginalCost = originalPrice,
            DiscountCode = discount?.Code,
            DiscountAmount = discountAmount,
            Cost = finalPrice  // Final price after discount
        };

        db.Reservations.Add(r);

        // Record discount usage if applied
        if (discount != null)
        {
            await DiscountValidationService.RecordDiscountUsageAsync(
                discount,
                reservationId,
                userId,
                originalPrice,
                discountAmount,
                finalPrice,
                db);
        }
        else
        {
            await db.SaveChangesAsync();
        }

        var response = new
        {
            status = "Success",
            reservation = new
            {
                id = r.Id,
                licenseplate = req.LicensePlate,
                startdate = startDate.ToString("yyyy-MM-dd"),
                enddate = endDate.ToString("yyyy-MM-dd"),
                parkinglot = req.ParkingLot.ToString(),
                originalCost = originalPrice,
                discountCode = discount?.Code,
                discountAmount = discountAmount,
                finalCost = finalPrice
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

    public static async Task<IResult> UpdateReservation(string id, AppDbContext db, ReservationRequest req, HttpContext http)
    {
        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null)
        {
            return Results.NotFound("Reservation not found.");
        }
        
        // Let op: Autorisatie (controleren of dit de reservering van de ingelogde gebruiker is) mist hier!

        if (string.IsNullOrWhiteSpace(req.LicensePlate) || !req.StartDate.HasValue || !req.EndDate.HasValue || req.ParkingLot <= 0)
        {
            return Results.BadRequest(new ErrorResponse
            {
                Error = "missing_fields",
                Message = "Bad request:/nLicensePlate, StartDate, EndDate, ParkingLot and VehicleId are required."
            });
        }
        var startDate = req.StartDate.Value;
        var endDate = req.EndDate.Value;
        if (endDate <= startDate)
        {
            return Results.BadRequest(new ErrorResponse { Error = "invalid_dates", Message = "Bad request:/nEndDate must be after StartDate." });
        }
        var parkingLot = await db.ParkingLots.FirstOrDefaultAsync(p => p.Id == req.ParkingLot);
        if (parkingLot is null)
            return Results.NotFound(new ErrorResponse { Error = "not_found", Message = "Parking lot not found." });

        // NEW: Validate and apply discount code if provided
        DiscountModel? discount = null;
        if (!string.IsNullOrWhiteSpace(req.DiscountCode))
        {
            var userId = ClaimHelper.GetUserId(http);
            var (isValid, message, validDiscount) =
                await DiscountValidationService.ValidateDiscountCodeAsync(
                    req.DiscountCode,
                    userId,
                    parkingLot.Id,
                    startDate,
                    endDate,
                    db);

            if (!isValid)
            {
                return Results.BadRequest(new { error = "invalid_discount", message });
            }

            discount = validDiscount;
        }

        var (originalPrice, discountAmount, finalPrice) =
            CalculateHelpers.CalculatePriceWithDiscount(parkingLot, startDate, endDate, discount);

        var vehicleId = ClaimHelper.LicensePlateHelper(http, req.LicensePlate, db);
        reservation.ParkingLotId = parkingLot.Id;
        reservation.VehicleId = vehicleId;
        reservation.StartTime = startDate;
        reservation.EndTime = endDate;
        reservation.OriginalCost = originalPrice;
        reservation.DiscountCode = discount?.Code;
        reservation.DiscountAmount = discountAmount;
        reservation.Cost = finalPrice;

        // Record usage if new discount applied
        if (discount != null)
        {
            await DiscountValidationService.RecordDiscountUsageAsync(
                discount, reservation.Id, ClaimHelper.GetUserId(http),
                originalPrice, discountAmount, finalPrice, db);
        }
        else
        {
            await db.SaveChangesAsync();
        }

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

    public static async Task<IResult> GetReservationById(string id, AppDbContext db, HttpContext http)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (reservation == null)
        {
            return Results.NotFound("Reservation not found.");
        }

        if (reservation.UserId != userId)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            reservation.Id,
            reservation.ParkingLotId,
            reservation.VehicleId,
            reservation.StartTime,
            reservation.EndTime,
            Status = reservation.Status.ToString(),
            reservation.Cost
        });
    }
}