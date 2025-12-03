using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

namespace V2.Handlers;

public static class BillingHandlers
{
    /// <summary>
    /// Get upcoming/pending payments for the authenticated user.
    /// Route: GET /billing
    /// </summary>
    public static async Task<IResult> GetUpcomingPayments(ClaimsPrincipal user, AppDbContext db)
    {
        try
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            // 1. Fetch data WITHOUT sorting first (to avoid SQLite DateTimeOffset error)
            var rawReservations = await db.Reservations
                .AsQueryable()
                .Where(r => r.UserId == userId && 
                            r.Status == ReservationStatus.confirmed && 
                            r.Cost > 0)
                .Select(r => new
                {
                    Type = "Reservation",
                    ReservationId = r.Id,
                    ParkingLotId = r.ParkingLotId,
                    VehicleId = r.VehicleId,
                    Amount = (decimal)r.Cost,
                    Description = $"Reservation at Lot {r.ParkingLotId}",
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    CreatedAt = r.CreatedAt,
                    Status = r.Status.ToString()
                })
                .ToListAsync();

            // 2. Perform sorting in memory
            var sortedReservations = rawReservations
                .OrderByDescending(r => r.StartTime)
                .ToList();

            var totalAmount = sortedReservations.Sum(r => (decimal)r.Amount);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    upcomingPayments = sortedReservations,
                    summary = new
                    {
                        totalAmount = totalAmount,
                        count = sortedReservations.Count
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetUpcomingPayments: {ex.Message}");
            return Results.StatusCode(500);
        }
    }

    /// <summary>
    /// Get billing/payment history for the authenticated user.
    /// Route: GET /billing/history
    /// </summary>
    public static async Task<IResult> GetBillingHistory(ClaimsPrincipal user, AppDbContext db)
    {
        try
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            string initiatorId = userId.ToString();

            // 1. Fetch data WITHOUT sorting first
            var rawHistory = await db.Payments
                .AsQueryable()
                .Where(p => p.Initiator == initiatorId)
                .Select(p => new
                {
                    TransactionId = p.Transaction,
                    Amount = (decimal)p.Amount,
                    TaxAmount = (decimal)p.TAmount,
                    Method = p.Method,
                    Bank = p.Bank,
                    Issuer = p.Issuer,
                    Status = "Completed",
                    Date = p.CreatedAt,
                    CompletedAt = p.CompletedAt,
                    TaxDate = p.TDate
                })
                .ToListAsync();

            // 2. Perform sorting in memory
            var sortedHistory = rawHistory
                .OrderByDescending(p => p.Date)
                .ToList();

            var totalPaid = sortedHistory.Sum(p => (decimal)p.Amount);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    billingHistory = sortedHistory,
                    summary = new
                    {
                        totalPaid = totalPaid,
                        totalTransactions = sortedHistory.Count
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetBillingHistory: {ex.Message}");
            return Results.StatusCode(500);
        }
    }
}