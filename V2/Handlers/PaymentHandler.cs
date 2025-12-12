using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;

public   class PaymentHandler
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
    

    public static async Task<IResult> Createpayment(HttpContext http, AppDbContext db, CreatePaymentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ReservationId) || string.IsNullOrWhiteSpace(req.Method))
        {
            return Results.BadRequest("ReservationId and Method are required.");
        }

        var userIdClaim = http.User?.Claims
            .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Results.Unauthorized();

        if (!int.TryParse(userIdClaim, out int userId))
            return Results.BadRequest("Invalid user ID in token.");

        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == req.ReservationId);
        if (reservation == null)
            return Results.NotFound("Reservation not found.");

        if (reservation.UserId != userId)
            return Results.Forbid();

        var amount = reservation.Cost;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var initiator = user?.Username ?? userId.ToString();

        var now = DateTimeOffset.UtcNow;
        var transactionId = Guid.NewGuid().ToString("N");
        var hash = Guid.NewGuid().ToString();

        var payment = new Payment
        {
            Transaction = transactionId,
            Amount = amount,
            Initiator = initiator,
            CreatedAt = now,
            CompletedAt = now,
            Hash = hash,
            TAmount = amount,
            TDate = now,
            Method = req.Method,
            Issuer = string.Empty,
            Bank = string.Empty,
            ReservationId = reservation.Id,
            Status = PaymentStatus.Completed
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var response = new
        {
            payment.Transaction,
            payment.Amount,
            payment.Method,
            payment.Status,
            payment.ReservationId,
            payment.CreatedAt,
            payment.CompletedAt
        };

        return Results.Created($"/payments/{payment.Transaction}", response);
    }
}