using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Helpers;

public static class VehicleHandlers
{
    public static async Task<IResult> CreateVehicle(HttpContext http, VehicleModel vehicle, AppDbContext db)
    {
        var check = await ActiveAccountHelper.CheckActive(http, db);
        if (check != null) return check;

        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var nextID = db.Vehicles.Any() ? await db.Vehicles.MaxAsync(v => v.Id) + 1 : 1;
        vehicle.Id = nextID;
        vehicle.UserId = userId;
        vehicle.CreatedAt = DateOnly.FromDateTime(DateTime.Now);

        if (string.IsNullOrWhiteSpace(vehicle.LicensePlate))
            return Results.BadRequest("License plate is required.");
        if (string.IsNullOrWhiteSpace(vehicle.Model))
            return Results.BadRequest("Model is required.");
        if (string.IsNullOrWhiteSpace(vehicle.Color))
            return Results.BadRequest("Color is required.");
        if (string.IsNullOrWhiteSpace(vehicle.Make))
            return Results.BadRequest("Make is required.");

        if (await db.Vehicles.AnyAsync(v => v.LicensePlate == vehicle.LicensePlate))
            return Results.Conflict("A vehicle with this license plate already exists.");

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        return Results.Created($"/vehicles/{vehicle.Id}", vehicle);
    }

    public static async Task<IResult> GetMyVehicles(HttpContext http, AppDbContext db)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        var vehicles = await db.Vehicles
            .Where(v => v.UserId == userId)
            .ToListAsync();

        return Results.Ok(vehicles);
    }

    public static async Task<IResult> UpdateVehicle(int id, HttpContext http, VehicleModel updatedVehicle, AppDbContext db)
    {
        var check = await ActiveAccountHelper.CheckActive(http, db);
        if (check != null) return check;

        var vehicle = await db.Vehicles.FindAsync(id);
        if (vehicle == null)
            return Results.NotFound("Vehicle not found.");

        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0 || vehicle.UserId != userId)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(updatedVehicle.LicensePlate))
            return Results.BadRequest("License plate is required.");
        if (string.IsNullOrWhiteSpace(updatedVehicle.Model))
            return Results.BadRequest("Model is required.");
        if (string.IsNullOrWhiteSpace(updatedVehicle.Color))
            return Results.BadRequest("Color is required.");
        if (string.IsNullOrWhiteSpace(updatedVehicle.Make))
            return Results.BadRequest("Make is required.");

        if (await db.Vehicles.AnyAsync(v => v.LicensePlate == updatedVehicle.LicensePlate && v.Id != id))
            return Results.Conflict("A vehicle with this license plate already exists.");

        vehicle.LicensePlate = updatedVehicle.LicensePlate;
        vehicle.Model = updatedVehicle.Model;
        vehicle.Color = updatedVehicle.Color;
        vehicle.Make = updatedVehicle.Make;

        await db.SaveChangesAsync();

        return Results.Ok(vehicle);
    }

    public static async Task<IResult> DeleteVehicle(int id, HttpContext http, AppDbContext db)
    {
        var check = await ActiveAccountHelper.CheckActive(http, db);
        if (check != null) return check;

        var vehicle = await db.Vehicles.FindAsync(id);
        if (vehicle == null)
            return Results.NotFound("Vehicle not found.");

        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0 || vehicle.UserId != userId)
        {
            return Results.Unauthorized();
        }

        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync();

        return Results.Ok(new { status = "Success", message = "Vehicle deleted successfully." });
    }
}