using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;

namespace V2.Services;

public static class DiscountValidationService
{
    /// <summary>
    /// Validates if a discount code can be used for a specific reservation
    /// </summary>
    public static async Task<(bool isValid, string message, DiscountModel? discount)>
        ValidateDiscountCodeAsync(
            string code,
            int userId,
            int parkingLotId,
            DateTime startDate,
            DateTime endDate,
            AppDbContext db)
    {
        // Normalize code (trim and uppercase for case-insensitive comparison)
        code = code.Trim().ToUpperInvariant();

        // 1. Check if discount exists and is active
        var discount = await db.Set<DiscountModel>()
            .FirstOrDefaultAsync(d => d.Code.ToUpper() == code && d.IsActive);

        if (discount == null)
            return (false, "Discount code not found or inactive.", null);

        // 2. Check expiration
        if (discount.ValidUntil < DateTimeOffset.UtcNow)
            return (false, "Discount code has expired.", null);

        // 3. Check usage limit
        if (discount.MaxUsageCount.HasValue &&
            discount.CurrentUsageCount >= discount.MaxUsageCount.Value)
            return (false, "Discount code has reached maximum usage limit.", null);

        // 4. Check user restriction
        if (discount.UserId.HasValue && discount.UserId.Value != userId)
            return (false, "This discount code is not available for your account.", null);

        // 5. Check organization restriction
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return (false, "User not found.", null);

        if (discount.OrganizationId.HasValue &&
            user.OrganizationId != discount.OrganizationId.Value)
            return (false, "This discount code is only available for specific organizations.", null);

        // 6. Check parking lot restriction
        if (discount.ParkingLotId.HasValue &&
            discount.ParkingLotId.Value != parkingLotId)
            return (false, "This discount code is not valid for this parking lot.", null);

        // 7. Check reservation duration restrictions
        var duration = endDate - startDate;

        if (discount.MinReservationDuration.HasValue &&
            duration < discount.MinReservationDuration.Value)
        {
            var minHours = discount.MinReservationDuration.Value.TotalHours;
            return (false, $"Minimum reservation duration for this code is {minHours:F1} hours.", null);
        }

        if (discount.MaxReservationDuration.HasValue &&
            duration > discount.MaxReservationDuration.Value)
        {
            var maxHours = discount.MaxReservationDuration.Value.TotalHours;
            return (false, $"Maximum reservation duration for this code is {maxHours:F1} hours.", null);
        }

        return (true, "Discount code is valid.", discount);
    }

    /// <summary>
    /// Calculates the discount amount based on discount type
    /// </summary>
    public static decimal CalculateDiscountAmount(
        DiscountModel discount,
        decimal originalAmount)
    {
        if (discount.Type == DiscountType.Percentage)
        {
            // Calculate percentage discount
            return Math.Round(originalAmount * (discount.Percentage / 100m), 2);
        }
        else // FixedAmount
        {
            // Fixed amount discount, but never exceed original amount
            return Math.Min(discount.FixedAmount ?? 0, originalAmount);
        }
    }

    /// <summary>
    /// Records discount usage and increments usage count
    /// </summary>
    public static async Task RecordDiscountUsageAsync(
        DiscountModel discount,
        string reservationId,
        int userId,
        decimal originalAmount,
        decimal discountAmount,
        decimal finalAmount,
        AppDbContext db)
    {
        // Create usage record for audit trail
        var usage = new DiscountUsageModel
        {
            DiscountId = discount.Id,
            Code = discount.Code,
            ReservationId = reservationId,
            UserId = userId,
            OriginalAmount = originalAmount,
            DiscountAmount = discountAmount,
            FinalAmount = finalAmount,
            UsedAt = DateTimeOffset.UtcNow
        };

        db.Set<DiscountUsageModel>().Add(usage);

        // Increment usage count on the discount
        discount.CurrentUsageCount++;

        await db.SaveChangesAsync();
    }
}
