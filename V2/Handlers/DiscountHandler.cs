using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Services;
using V2.Helpers;

namespace V2.Handlers;

public static class DiscountHandler
{
    /// <summary>
    /// POST /admin/discounts - Create new discount code (ADMIN only)
    /// </summary>
    public static async Task<IResult> CreateDiscount(
        HttpContext http,
        AppDbContext db,
        CreateDiscountRequest req)
    {
        // Validation: Code is required
        if (string.IsNullOrWhiteSpace(req.Code))
            return Results.BadRequest("Discount code is required.");

        // Normalize code
        var code = req.Code.Trim().ToUpperInvariant();

        // Validation: Type-specific validation
        if (req.Type == DiscountType.Percentage)
        {
            if (!req.Percentage.HasValue || req.Percentage <= 0 || req.Percentage > 100)
                return Results.BadRequest("Percentage must be between 0 and 100.");
        }
        else if (req.Type == DiscountType.FixedAmount)
        {
            if (!req.FixedAmount.HasValue || req.FixedAmount <= 0)
                return Results.BadRequest("Fixed amount must be greater than 0.");
        }

        // Validation: ValidUntil must be in the future
        if (req.ValidUntil <= DateTimeOffset.UtcNow)
            return Results.BadRequest("ValidUntil must be in the future.");

        // Check if code already exists
        var existing = await db.Set<DiscountModel>()
            .FirstOrDefaultAsync(d => d.Code.ToUpper() == code);

        if (existing != null)
            return Results.Conflict("Discount code already exists.");

        // Get admin username for audit
        var adminUsername = http.User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

        // Create discount
        var newDiscount = new DiscountModel
        {
            Code = code,
            Type = req.Type,
            Percentage = req.Percentage ?? 0,
            FixedAmount = req.FixedAmount,
            ValidUntil = req.ValidUntil,
            MaxUsageCount = req.MaxUsageCount,
            CurrentUsageCount = 0,
            IsActive = true,
            UserId = req.UserId,
            OrganizationId = req.OrganizationId,
            ParkingLotId = req.ParkingLotId,
            MinReservationDuration = req.MinReservationDuration,
            MaxReservationDuration = req.MaxReservationDuration,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = adminUsername
        };

        db.Set<DiscountModel>().Add(newDiscount);
        await db.SaveChangesAsync();

        return Results.Created($"/admin/discounts/{newDiscount.Id}", newDiscount);
    }

    /// <summary>
    /// GET /admin/discounts - List all discount codes (ADMIN only)
    /// </summary>
    public static async Task<IResult> GetAllDiscounts(
        AppDbContext db,
        bool? active = null,
        bool? includeExpired = null)
    {
        var query = db.Set<DiscountModel>().AsQueryable();

        // Filter by active status if specified
        if (active.HasValue)
            query = query.Where(d => d.IsActive == active.Value);

        // Execute query effectively switching to client-side evaluation for DateTimeOffset operations
        // which are not supported by the LibSql provider in Where/OrderBy clauses
        var dbDiscounts = await query.ToListAsync();
        var result = dbDiscounts.AsEnumerable();

        // Filter out expired codes unless explicitly requested
        if (includeExpired == false || !includeExpired.HasValue)
        {
            var now = DateTimeOffset.UtcNow;
            result = result.Where(d => d.ValidUntil > now);
        }

        var discounts = result
            .OrderByDescending(d => d.Id)
            .Select(d => new DiscountResponse
            {
                Id = d.Id,
                Code = d.Code,
                Type = d.Type,
                Percentage = d.Percentage,
                FixedAmount = d.FixedAmount,
                ValidUntil = d.ValidUntil,
                IsActive = d.IsActive,
                CurrentUsageCount = d.CurrentUsageCount,
                MaxUsageCount = d.MaxUsageCount
            })
            .ToList();

        return Results.Ok(discounts);
    }

    /// <summary>
    /// GET /admin/discounts/{code} - Get specific discount by code (ADMIN only)
    /// </summary>
    public static async Task<IResult> GetDiscountByCode(string code, AppDbContext db)
    {
        code = code.Trim().ToUpperInvariant();

        var discount = await db.Set<DiscountModel>()
            .FirstOrDefaultAsync(d => d.Code.ToUpper() == code);

        if (discount == null)
            return Results.NotFound("Discount code not found.");

        var response = new DiscountDetailResponse
        {
            Id = discount.Id,
            Code = discount.Code,
            Type = discount.Type,
            Percentage = discount.Percentage,
            FixedAmount = discount.FixedAmount,
            ValidUntil = discount.ValidUntil,
            IsActive = discount.IsActive,
            CurrentUsageCount = discount.CurrentUsageCount,
            MaxUsageCount = discount.MaxUsageCount,
            UserId = discount.UserId,
            OrganizationId = discount.OrganizationId,
            ParkingLotId = discount.ParkingLotId,
            MinReservationDuration = discount.MinReservationDuration,
            MaxReservationDuration = discount.MaxReservationDuration,
            CreatedAt = discount.CreatedAt,
            CreatedBy = discount.CreatedBy
        };

        return Results.Ok(response);
    }

    /// <summary>
    /// PUT /admin/discounts/{id} - Update discount (ADMIN only)
    /// </summary>
    public static async Task<IResult> UpdateDiscount(
        int id,
        AppDbContext db,
        UpdateDiscountRequest req)
    {
        var discount = await db.Set<DiscountModel>().FindAsync(id);

        if (discount == null)
            return Results.NotFound("Discount not found.");

        // Update only provided fields
        if (req.IsActive.HasValue)
            discount.IsActive = req.IsActive.Value;

        if (req.ValidUntil.HasValue)
        {
            if (req.ValidUntil.Value <= DateTimeOffset.UtcNow)
                return Results.BadRequest("ValidUntil must be in the future.");
            discount.ValidUntil = req.ValidUntil.Value;
        }

        if (req.MaxUsageCount.HasValue)
            discount.MaxUsageCount = req.MaxUsageCount.Value;

        await db.SaveChangesAsync();

        return Results.Ok(discount);
    }

    /// <summary>
    /// DELETE /admin/discounts/{id} - Deactivate discount (soft delete) (ADMIN only)
    /// </summary>
    public static async Task<IResult> DeactivateDiscount(int id, AppDbContext db)
    {
        var discount = await db.Set<DiscountModel>().FindAsync(id);

        if (discount == null)
            return Results.NotFound("Discount not found.");

        discount.IsActive = false;
        await db.SaveChangesAsync();

        return Results.Ok(new { status = "Success", message = "Discount code deactivated successfully." });
    }

    /// <summary>
    /// GET /admin/discounts/{id}/stats - Get usage statistics (ADMIN only)
    /// </summary>
    public static async Task<IResult> GetDiscountStatistics(int id, AppDbContext db)
    {
        var discount = await db.Set<DiscountModel>().FindAsync(id);

        if (discount == null)
            return Results.NotFound("Discount not found.");

        // Get usage records
        var usagesList = await db.Set<DiscountUsageModel>()
            .Where(u => u.DiscountId == id)
            .ToListAsync();

        var usages = usagesList
            .OrderByDescending(u => u.UsedAt)
            .ToList();

        // Calculate statistics
        var stats = new DiscountStatisticsResponse
        {
            Id = discount.Id,
            Code = discount.Code,
            TimesUsed = usages.Count,
            TotalAmountSaved = usages.Sum(u => u.DiscountAmount),
            AverageDiscountAmount = usages.Any() ? usages.Average(u => u.DiscountAmount) : 0,
            UniqueUsers = usages.Select(u => u.UserId).Distinct().Count(),
            FirstUsed = usages.Any() ? usages.Min(u => u.UsedAt) : null,
            LastUsed = usages.Any() ? usages.Max(u => u.UsedAt) : null,
            RecentUsages = usages.Take(10).Select(u => new DiscountUsageRecord
            {
                ReservationId = u.ReservationId,
                UserId = u.UserId,
                Username = db.Users.FirstOrDefault(user => user.Id == u.UserId)?.Name ?? "Unknown",
                DiscountAmount = u.DiscountAmount,
                UsedAt = u.UsedAt
            }).ToList()
        };

        return Results.Ok(stats);
    }

    /// <summary>
    /// GET /discounts/validate/{code} - Validate discount code (USER)
    /// </summary>
    public static async Task<IResult> ValidateDiscountForUser(
        string code,
        HttpContext http,
        AppDbContext db,
        int? parkingLotId = null,
        decimal? amount = null)
    {
        var userId = ClaimHelper.GetUserId(http);
        if (userId == 0)
            return Results.Unauthorized();

        code = code.Trim().ToUpperInvariant();

        // Basic validation without full reservation context
        var discount = await db.Set<DiscountModel>()
            .FirstOrDefaultAsync(d => d.Code.ToUpper() == code && d.IsActive);

        if (discount == null)
        {
            return Results.NotFound("Discount code not found or inactive.");
        }

        // Check expiration
        if (discount.ValidUntil < DateTimeOffset.UtcNow)
        {
            return Results.BadRequest("Discount code has expired.");
        }

        // Check usage limit
        if (discount.MaxUsageCount.HasValue &&
            discount.CurrentUsageCount >= discount.MaxUsageCount.Value)
        {
            return Results.BadRequest("Discount code has reached maximum usage limit.");
        }

        // Calculate estimated discount if amount provided
        decimal estimatedDiscount = 0;
        if (amount.HasValue && amount.Value > 0)
        {
            estimatedDiscount = DiscountValidationService.CalculateDiscountAmount(discount, amount.Value);
        }

        return Results.Ok(new DiscountValidationResponse
        {
            IsValid = true,
            Code = discount.Code,
            DiscountAmount = estimatedDiscount,
            Message = "Discount code is valid.",
            Type = discount.Type,
            Value = discount.Type == DiscountType.Percentage ? discount.Percentage : discount.FixedAmount
        });
    }
}