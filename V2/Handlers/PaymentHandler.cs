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

    public record AdminUpdatePaymentRequest(
    decimal? Amount,
    string? Method,
    PaymentStatus? Status
);

    public static async Task<IResult> AdminCancelUserPayment(
        AppDbContext db,
        string transaction)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Transaction == transaction);
        if (payment == null)
            return Results.NotFound("Payment not found.");

        if (payment.Status == PaymentStatus.Pending)
        {
            payment.Status = PaymentStatus.Failed;
            payment.CompletedAt = DateTimeOffset.UtcNow;
        }
        else if (payment.Status == PaymentStatus.Completed)
        {
            payment.Status = PaymentStatus.Refunded;
            payment.CompletedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            return Results.BadRequest($"Cannot cancel payment with status '{payment.Status}'.");
        }

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            payment.Transaction,
            payment.Amount,
            payment.Method,
            payment.Status,
            payment.ReservationId,
            payment.CreatedAt,
            payment.CompletedAt
        });
    }

    public static async Task<IResult> AdminUpdatePayment(
        AppDbContext db,
        string transaction,
        AdminUpdatePaymentRequest req)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Transaction == transaction);
        if (payment == null)
            return Results.NotFound("Payment not found.");

        if (req.Amount.HasValue)
        {
            if (req.Amount.Value <= 0)
                return Results.BadRequest("Amount must be > 0.");

            payment.Amount = req.Amount.Value;
            payment.TAmount = req.Amount.Value;
        }

        if (!string.IsNullOrWhiteSpace(req.Method))
            payment.Method = req.Method;

        if (req.Status.HasValue)
        {
            payment.Status = req.Status.Value;

            if (payment.Status != PaymentStatus.Pending)
                payment.CompletedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            payment.Transaction,
            payment.Amount,
            payment.Method,
            payment.Status,
            payment.ReservationId,
            payment.CreatedAt,
            payment.CompletedAt
        });
    }

    public record OrgRoleRequest(string OrganizationRole);

    public static async Task<IResult> AdminAssignUserToOrganization(
        int orgId,
        int userId,
        OrgRoleRequest req,
        AppDbContext db)
    {
        var role = (req.OrganizationRole ?? "").Trim().ToUpperInvariant();
        if (role != "ADMIN" && role != "EMPLOYEE")
            return Results.BadRequest("OrganizationRole must be 'ADMIN' or 'EMPLOYEE'.");

        var orgExists = await db.Organizations.AnyAsync(o => o.Id == orgId);
        if (!orgExists) return Results.NotFound("Organization not found.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Results.NotFound("User not found.");

        user.OrganizationId = orgId;
        user.OrganizationRole = role;

        var vehicles = await db.Vehicles.Where(v => v.UserId == userId).ToListAsync();
        foreach (var v in vehicles)
            v.OrganizationId = orgId;

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "User assigned to organization.",
            userId = user.Id,
            organizationId = orgId,
            organizationRole = user.OrganizationRole,
            vehiclesUpdated = vehicles.Count
        });
    }

    public static async Task<IResult> AdminRemoveUserFromOrganization(
        int orgId,
        int userId,
        AppDbContext db)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Results.NotFound("User not found.");

        if (user.OrganizationId != orgId)
            return Results.Conflict("User is not assigned to this organization.");

        user.OrganizationId = null;
        user.OrganizationRole = null;

        var vehicles = await db.Vehicles.Where(v => v.UserId == userId).ToListAsync();
        foreach (var v in vehicles)
            v.OrganizationId = null;

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "User removed from organization.",
            userId = user.Id,
            organizationId = orgId,
            vehiclesUpdated = vehicles.Count
        });
    }

    public static async Task<IResult> AdminUpdateUserOrganizationRole(
        int orgId,
        int userId,
        OrgRoleRequest req,
        AppDbContext db)
    {
        var role = (req.OrganizationRole ?? "").Trim().ToUpperInvariant();
        if (role != "ADMIN" && role != "EMPLOYEE")
            return Results.BadRequest("OrganizationRole must be 'ADMIN' or 'EMPLOYEE'.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Results.NotFound("User not found.");

        if (user.OrganizationId != orgId)
            return Results.Conflict("User is not assigned to this organization.");

        user.OrganizationRole = role;
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = "Organization role updated.",
            userId = user.Id,
            organizationId = orgId,
            organizationRole = user.OrganizationRole
        });
    }

}
