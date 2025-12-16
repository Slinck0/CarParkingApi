using Microsoft.AspNetCore.StaticAssets;
using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;
public class PaymentHandler
{
    public static async Task<IResult> GetPayments(HttpContext http, AppDbContext db)
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


    public static async Task<IResult> CreatePayment(HttpContext http, AppDbContext db, CreatePaymentRequest req)
    {
        
        if (string.IsNullOrWhiteSpace(req.Method))
            return Results.BadRequest("Payment Method is required.");

        if (string.IsNullOrWhiteSpace(req.ReservationId) && string.IsNullOrWhiteSpace(req.ParkingSessionId))
            return Results.BadRequest("Either ReservationId or ParkingSessionId must be provided.");

        var userIdClaim = http.User?.Claims
            .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Results.Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Results.Unauthorized();

        decimal originalAmount = 0;
        
        Reservation? reservationToUpdate = null;
        ParkingSessions ? sessionToUpdate = null;

        if (!string.IsNullOrWhiteSpace(req.ReservationId))
        {
            reservationToUpdate = await db.Reservations.FirstOrDefaultAsync(r => r.Id == req.ReservationId);
            
            if (reservationToUpdate == null) return Results.NotFound("Reservation not found.");
            if (reservationToUpdate.UserId != userId) return Results.Forbid();
            if (reservationToUpdate.Status == ReservationStatus.paid) return Results.BadRequest("Already paid.");

            originalAmount = (decimal)reservationToUpdate.Cost;
        }
        else if (!string.IsNullOrWhiteSpace(req.ParkingSessionId))
        {
            if (!int.TryParse(req.ParkingSessionId, out int pSessionId)) 
                return Results.BadRequest("Invalid ParkingSession ID format.");
            
            sessionToUpdate = await db.ParkingSessions.FirstOrDefaultAsync(p => p.Id == pSessionId);

            if (sessionToUpdate == null) return Results.NotFound("Parking session not found.");
            if (sessionToUpdate.UserId != userId) return Results.Forbid();
            

            originalAmount = (decimal)sessionToUpdate.Cost;
        }

        // 3. Discount Toepassen (Via Database Lookup op basis van jouw DiscountModel)
        decimal finalAmount = originalAmount;
        
        if (!string.IsNullOrWhiteSpace(req.DiscountCode))
        {
            // Zorg dat je 'Discounts' DbSet in je AppDbContext hebt staan!
            var discount = await db.Discounts
                .FirstOrDefaultAsync(d => d.Code == req.DiscountCode);
            
            if (discount != null)
            {
                // Check of korting nog geldig is (ValidUntil is DateTimeOffset volgens jouw model)
                if (discount.ValidUntil >= DateTimeOffset.UtcNow)
                {
                    // Jouw model heeft 'Percentage'
                    if (discount.Percentage > 0)
                    {
                        finalAmount = originalAmount * (1 - (discount.Percentage / 100m));
                    }
                }
            }
        }
        
        // Zorg dat het bedrag niet negatief wordt
        if (finalAmount < 0) finalAmount = 0;

        // 4. Betaling aanmaken
        var initiator = user.Username ?? userId.ToString();
        var now = DateTimeOffset.UtcNow;
        var transactionId = Guid.NewGuid().ToString("N");
        var hash = Guid.NewGuid().ToString();

        var payment = new Payment
        {
            Transaction = transactionId,
            Amount = finalAmount,    // Het bedrag MET korting
            TAmount = originalAmount, // Het originele bedrag
            
            Initiator = initiator,
            CreatedAt = now,
            CompletedAt = now,
            Hash = hash,
            TDate = now,
            Method = req.Method,
            Issuer = string.Empty,
            Bank = string.Empty,
            Status = PaymentStatus.Completed,

            ReservationId = reservationToUpdate?.Id,
            // ParkingSessionId = sessionToUpdate?.Id 
        };

        db.Payments.Add(payment);

        // 5. Update de status van het item naar 'Paid'
        if (reservationToUpdate != null)
        {
            reservationToUpdate.Status = ReservationStatus.paid;
        }
        if (sessionToUpdate != null)
        {
            sessionToUpdate.Status = "Paid";
        }

        await db.SaveChangesAsync();

        // 6. Response
        var response = new
        {
            payment.Transaction,
            PaidAmount = payment.Amount,
            OriginalAmount = originalAmount,
            DiscountCode = req.DiscountCode,
            DiscountApplied = originalAmount > finalAmount,
            payment.Method,
            payment.Status,
            payment.ReservationId,
            payment.CreatedAt
        };

        return Results.Created($"/payments/{payment.Transaction}", response);
    }


    public static async Task<IResult> UpcomingPayments(HttpContext http, AppDbContext db)
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
}
    