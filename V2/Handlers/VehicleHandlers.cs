using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using System.Text.RegularExpressions;

public static class VehicleHandlers
{
   
    public static async Task<IResult> CreateVehicle(HttpContext http, VehicleModel vehicle, AppDbContext db)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(vehicle.LicensePlate))
            return Results.BadRequest("License plate is required.");
        if (string.IsNullOrWhiteSpace(vehicle.Model))
            return Results.BadRequest("Model is required.");
        if (string.IsNullOrWhiteSpace(vehicle.Color))
            return Results.BadRequest("Color is required.");
        if (string.IsNullOrWhiteSpace(vehicle.Make))
            return Results.BadRequest("Make is required.");

        var currentYear = DateTime.Now.Year;
        if (vehicle.Year < 1900 || vehicle.Year > currentYear + 1)
        {
            return Results.BadRequest($"Year must be between 1900 and {currentYear + 1}.");
        }

        vehicle.LicensePlate = vehicle.LicensePlate.Trim().ToUpper();
        var licensePattern = @"^[A-Z0-9]{2}-[A-Z0-9]{2}-[A-Z0-9]{2}$";

        if (!Regex.IsMatch(vehicle.LicensePlate, licensePattern))
        {
            return Results.BadRequest("Invalid license plate format. Expected format: XX-XX-XX");
        }

        if (await db.Vehicles.AnyAsync(v => v.LicensePlate == vehicle.LicensePlate))
            return Results.Conflict("A vehicle with this license plate already exists.");

        var nextID = db.Vehicles.Any() ? await db.Vehicles.MaxAsync(v => v.Id) + 1 : 1;
        vehicle.Id = nextID;
        vehicle.UserId = userId;
        vehicle.CreatedAt = DateOnly.FromDateTime(DateTime.Now);

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

    public static async Task<IResult> AdminGetOrganizationVehicles(
    int orgId,
    AppDbContext db)
    {
        var orgExists = await db.Organizations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return Results.NotFound("Organization not found.");

        var vehicles = await db.Vehicles
            .AsNoTracking()
            .Where(v => v.OrganizationId == orgId)
            .Select(v => new
            {
                v.Id,
                v.UserId,
                v.OrganizationId,
                v.LicensePlate,
                v.Make,
                v.Model,
                v.Color,
                v.Year,
                v.CreatedAt
            })
            .ToListAsync();

        return Results.Ok(new
        {
            organizationId = orgId,
            count = vehicles.Count,
            vehicles
        });
    }

}