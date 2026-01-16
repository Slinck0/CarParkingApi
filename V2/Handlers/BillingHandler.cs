using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;


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
    


    public static async Task<IResult> GetBillingHistory(HttpContext http, AppDbContext db)
    {
        var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

       
        var paidReservations = await db.Reservations
            .Where(r => r.UserId == userId && r.Status == ReservationStatus.paid)
            .Select(r => new HistoryItemDto
            {
                Id = r.Id, 
                Type = "Reservation",
                Amount = r.Cost, 
                Date = r.CreatedAt, 
                Status = r.Status.ToString(),
                Description = "Reservation"
            }).ToListAsync();

        
        var rawSessions = await db.ParkingSessions
            .Where(p => p.UserId == userId && (p.Status == "Paid" || p.Status == "completed")) 
            .Select(p => new 
            {
                p.Id,
                p.Cost,
                p.EndTime,
                p.Status,
                p.LicensePlate
            })
            .ToListAsync();

        
        var paidSessions = rawSessions.Select(p => new HistoryItemDto
        {
            Id = p.Id.ToString(),
            Type = "ParkingSession",
            Amount = (decimal)p.Cost,
            Date = p.EndTime ?? DateTimeOffset.UtcNow, 
            Status = p.Status ?? "Paid",
            Description = $"Parking session {p.LicensePlate}"
        }).ToList();

        // STAP 4: Samenvoegen en sorteren
        var combinedHistory = paidReservations
            .Concat(paidSessions)
            .OrderByDescending(x => x.Date)
            .ToList();

        return Results.Ok(combinedHistory);
    }



} 