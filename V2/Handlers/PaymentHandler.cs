using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

public static class PaymentHandlers
{
    public static async Task<IResult> CreatePayment(
        HttpContext http,
        AppDbContext db,
        CreatePaymentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ReservationId) ||
            string.IsNullOrWhiteSpace(req.Method))
        {
            return Results.BadRequest("Invalid payment data.");
        }

        var userIdClaim = http.User?.Claims
            .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Results.Unauthorized();

        if (!int.TryParse(userIdClaim, out int userId))
            return Results.BadRequest("Invalid user ID.");

        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == req.ReservationId);
        if (reservation == null)
            return Results.NotFound("Reservation not found.");

        if (reservation.UserId != userId)
            return Results.Forbid();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var now = DateTimeOffset.UtcNow;

        var payment = new PaymentModel
        {
            Transaction = Guid.NewGuid().ToString("N"),
            Amount = reservation.Cost,
            Initiator = user?.Username ?? userId.ToString(),
            CreatedAt = now,
            CompletedAt = now,
            Hash = Guid.NewGuid().ToString(),

            TAmount = reservation.Cost,
            TDate = now,
            Method = req.Method,
            Issuer = string.Empty,
            Bank = string.Empty,

            ReservationId = reservation.Id,
            Status = PaymentStatus.Completed
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return Results.Created($"/payments/{payment.Transaction}", new
        {
            message = "Payment created successfully.",
            payment.Transaction,
            payment.Amount,
            payment.Method,
            payment.Status,
            payment.ReservationId,
            payment.CreatedAt,
            payment.CompletedAt
        });
    }

    public static async Task<IResult> GetUserNonCompletedPayments(
    HttpContext http,
    AppDbContext db)
    {
        var userIdClaim = http.User?.Claims
            .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Results.Unauthorized();

        if (!int.TryParse(userIdClaim, out int userId))
            return Results.BadRequest("Invalid user ID.");

        var payments = await (
            from p in db.Payments
            join r in db.Reservations on p.ReservationId equals r.Id
            where r.UserId == userId
               && p.Status != PaymentStatus.Completed
            select new
            {
                p.Transaction,
                p.Amount,
                p.Method,
                p.Status,
                p.CreatedAt,
                p.CompletedAt,
                p.ReservationId
            }).ToListAsync();

        return Results.Ok(payments);
    }

    public static async Task<IResult> GetUserPayments(
        HttpContext http,
        AppDbContext db)
    {
        var userIdClaim = http.User?.Claims
            .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Results.Unauthorized();

        if (!int.TryParse(userIdClaim, out int userId))
            return Results.BadRequest("Invalid user ID.");

        var payments = await (
        from p in db.Payments
        join r in db.Reservations on p.ReservationId equals r.Id
        where r.UserId == userId
        && p.Status == PaymentStatus.Completed
        select new
        {
            p.Transaction,
            p.Amount,
            p.Method,
            p.Status,
            p.CreatedAt,
            p.CompletedAt,
            p.ReservationId
        }).ToListAsync();


        return Results.Ok(payments);
    }
}
