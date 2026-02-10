namespace V2.Models;

public enum DiscountType
{
    Percentage,
    FixedAmount
}

public class DiscountModel
{
    // Existing fields
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public decimal Percentage { get; set; }
    public DateTimeOffset ValidUntil { get; set; }

    // NEW FIELDS - Type and value
    public DiscountType Type { get; set; } = DiscountType.Percentage;
    public decimal? FixedAmount { get; set; }

    // NEW FIELDS - Usage limitations
    public int? MaxUsageCount { get; set; }  // null = unlimited
    public int CurrentUsageCount { get; set; } = 0;

    // NEW FIELDS - Active status
    public bool IsActive { get; set; } = true;

    // NEW FIELDS - User restrictions (null = available to all)
    public int? UserId { get; set; }
    public int? OrganizationId { get; set; }

    // NEW FIELDS - Location restrictions (null = all locations)
    public int? ParkingLotId { get; set; }

    // NEW FIELDS - Duration restrictions
    public TimeSpan? MinReservationDuration { get; set; }
    public TimeSpan? MaxReservationDuration { get; set; }

    // NEW FIELDS - Audit
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

// Enhanced create request
public record CreateDiscountRequest
{
    public string Code { get; set; } = null!;
    public DiscountType Type { get; set; } = DiscountType.Percentage;
    public decimal? Percentage { get; set; }
    public decimal? FixedAmount { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public int? MaxUsageCount { get; set; }
    public int? UserId { get; set; }
    public int? OrganizationId { get; set; }
    public int? ParkingLotId { get; set; }
    public TimeSpan? MinReservationDuration { get; set; }
    public TimeSpan? MaxReservationDuration { get; set; }
}

// Update discount (partial update)
public record UpdateDiscountRequest
{
    public bool? IsActive { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public int? MaxUsageCount { get; set; }
}

// List response
public record DiscountResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public DiscountType Type { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? FixedAmount { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public bool IsActive { get; set; }
    public int CurrentUsageCount { get; set; }
    public int? MaxUsageCount { get; set; }
}

// Detailed response with restrictions
public record DiscountDetailResponse : DiscountResponse
{
    public int? UserId { get; set; }
    public int? OrganizationId { get; set; }
    public int? ParkingLotId { get; set; }
    public TimeSpan? MinReservationDuration { get; set; }
    public TimeSpan? MaxReservationDuration { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

// Statistics response
public record DiscountStatisticsResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public int TimesUsed { get; set; }
    public decimal TotalAmountSaved { get; set; }
    public decimal AverageDiscountAmount { get; set; }
    public int UniqueUsers { get; set; }
    public DateTimeOffset? FirstUsed { get; set; }
    public DateTimeOffset? LastUsed { get; set; }
    public List<DiscountUsageRecord> RecentUsages { get; set; } = new();
}

// Usage record for statistics
public record DiscountUsageRecord
{
    public string ReservationId { get; set; } = null!;
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public decimal DiscountAmount { get; set; }
    public DateTimeOffset UsedAt { get; set; }
}

// Validation response for users
public record DiscountValidationResponse
{
    public bool IsValid { get; set; }
    public string Code { get; set; } = null!;
    public decimal DiscountAmount { get; set; }
    public string Message { get; set; } = null!;
    public DiscountType? Type { get; set; }
    public decimal? Value { get; set; }
}