using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

public class DiscountHandler
{
public static async Task<IResult> PostDiscount(HttpContext http, AppDbContext db, CreateDiscountRequest req)
{
  
    if (string.IsNullOrWhiteSpace(req.Code) || req.Percentage <= 0 || req.Percentage > 100)
    {
        return Results.BadRequest("Invalid discount data.");
    }

   
    var existing = await db.Set<DiscountModel>()
        .FirstOrDefaultAsync(d => d.Code == req.Code);
        
    if (existing != null)
    {
        return Results.Conflict("Discount code already exists.");
    }

    var newDiscount = new DiscountModel
    {
        Code = req.Code,
        Percentage = req.Percentage,
        ValidUntil = req.ValidUntil, 
   
    };

 
    db.Set<DiscountModel>().Add(newDiscount);
    await db.SaveChangesAsync();

    return Results.Created($"/discounts/{newDiscount.Id}", newDiscount);
}

    public static async Task<IResult> GetDiscounts(HttpContext http, AppDbContext db)
    {
        var discounts = await db.Set<DiscountModel>().ToListAsync();
        return Results.Ok(discounts);
    }
}