using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
public static class ParkingLotHandlers
{
    public static async Task<IResult> CreateParkingLot(ParkingLotCreate req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Capacity <= 0 || string.IsNullOrWhiteSpace(req.Location) || string.IsNullOrWhiteSpace(req.Address) || req.Tariff <= 0 || req.DayTariff == null || req.Lat == 0 || req.Lng == 0)
        {
            return Results.BadRequest("Invalid parking lot data.");
        }

        var parkingLot = new ParkingLotModel
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
    public static async Task<IResult> UpdateParkingLot(int id, ParkingLotCreate req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.Capacity <= 0 || string.IsNullOrWhiteSpace(req.Location) || string.IsNullOrWhiteSpace(req.Address) || req.Tariff <= 0 || req.DayTariff == null || req.Lat == 0 || req.Lng == 0)
        {
            return Results.BadRequest("Invalid parking lot data.");
        }

        var parkingLot = await db.ParkingLots.FindAsync(id);

        if (parkingLot is null)
        {
            return Results.NotFound(new { message = "Parking lot not found." });
        }


        parkingLot.Name = req.Name;
        parkingLot.Capacity = req.Capacity;
        parkingLot.Location = req.Location;
        parkingLot.Address = req.Address;
        parkingLot.Tariff = req.Tariff;
        parkingLot.DayTariff = req.DayTariff;
        parkingLot.Lat = req.Lat;
        parkingLot.Lng = req.Lng;
        parkingLot.Status = req.Status;
        parkingLot.ClosedReason = req.ClosedReason;
        parkingLot.ClosedDate = req.ClosedDate;


        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Parking lot updated successfully.",
            parkingLotId = parkingLot.Id,
            parkingLotName = parkingLot.Name
        });
    }

    public static async Task<IResult> DeleteParkingLot(int id, AppDbContext db)
    {
        var parkingLot = await db.ParkingLots.FindAsync(id);

        if (parkingLot is null)
        {
            return Results.NotFound(new { message = "Parking lot not found." });
        }

        db.ParkingLots.Remove(parkingLot);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = $"Parking lot '{parkingLot.Name}' (ID: {id}) has been deleted." });
    }

    public static async Task<IResult> AdminCreateParkingLotForOrganization(
    int orgId,
    ParkingLotCreate req,
    AppDbContext db)
    {
        var orgExists = await db.Organizations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return Results.NotFound("Organization not found.");

        if (string.IsNullOrWhiteSpace(req.Name) || req.Capacity <= 0 || string.IsNullOrWhiteSpace(req.Location) ||
            string.IsNullOrWhiteSpace(req.Address) || req.Tariff <= 0 || req.DayTariff == null || req.Lat == 0 || req.Lng == 0)
        {
            return Results.BadRequest("Invalid parking lot data.");
        }

        var parkingLot = new ParkingLotModel
        {
            OrganizationId = orgId,
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

        return Results.Created($"/admin/organizations/{orgId}/parking-lots/{parkingLot.Id}", new
        {
            message = "Parking lot created for organization.",
            organizationId = orgId,
            parkingLotId = parkingLot.Id,
            parkingLotName = parkingLot.Name
        });
    }

    public static async Task<IResult> AdminDeleteParkingLotFromOrganization(
        int orgId,
        int parkingLotId,
        AppDbContext db)
    {
        var parkingLot = await db.ParkingLots.FirstOrDefaultAsync(p => p.Id == parkingLotId);
        if (parkingLot is null) return Results.NotFound("Parking lot not found.");

        if (parkingLot.OrganizationId != orgId)
            return Results.Conflict("Parking lot does not belong to this organization.");

        db.ParkingLots.Remove(parkingLot);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}

