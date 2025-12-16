using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

namespace V2.Handlers;

public  class BillingHandlers
{
   
    public static async Task<IResult> GetUpcomingPayments(ClaimsPrincipal user, AppDbContext db, HttpContext http)
    {
        var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
            return Results.Unauthorized();
            
        if (!int.TryParse(userIdClaim, out int userId))
            return Results.BadRequest("Invalid user ID in token.");

        var allReservations = await db.Reservations
            .Where(r => r.UserId == userId)
            .ToListAsync();

        var reservationsList = allReservations
            .Where(r => r.Status != ReservationStatus.cancelled && r.Status != ReservationStatus.paid)
            .ToList();

        var AllparkingList = await db.ParkingSessions
            .Where(p => p.UserId == userId)
            .ToListAsync();

        var parkingList = AllparkingList
        .Where(p => p.Status != "cancelled" && p.Status != "Paid")
        .ToList();

        var combinedList = new List<object>();

        foreach (var r in reservationsList)
        {
            combinedList.Add(new 
            {
                Id = r.Id,
                Type = "Reservation",
                Date = r.StartTime.DateTime, 
                Amount = r.Cost,
                Status = r.Status.ToString()
            });
        }

        foreach (var p in parkingList)
        {
            combinedList.Add(new 
            {
                Id = p.Id,
                Type = "ParkingSession",
                Date = p.StartTime,
                Amount = p.Cost,
                Status = p.Status.ToString()
            });
        }
        var sortedList = combinedList
            .OrderBy(x => ((dynamic)x).Date)
            .ToList();

        return Results.Ok(sortedList);
    }
    


    public static async Task<IResult> GetBillingHistory(ClaimsPrincipal user, AppDbContext db, HttpContext http)
    {
        var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Results.Unauthorized();

        if (!int.TryParse(userIdClaim, out int userId))
            return Results.BadRequest("Invalid user ID in token.");

        var payments = await (
            from p in db.Payments
            join r in db.Reservations on p.ReservationId equals r.Id into pr
            from r in pr.DefaultIfEmpty()
            where r != null && r.UserId == userId
            select new
            {
                p.Transaction,
                p.Amount,
                p.Method,
                p.Status,
                p.CreatedAt,
                p.CompletedAt,
                ReservationId = p.ReservationId
            }).ToListAsync();

        return Results.Ok(payments);
    }
}