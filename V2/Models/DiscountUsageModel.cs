namespace V2.Models;

public class DiscountUsageModel
{
    public int Id { get; set; }
    public int DiscountId { get; set; }
    public string Code { get; set; } = null!;
    public string ReservationId { get; set; } = null!;
    public int UserId { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public DateTimeOffset UsedAt { get; set; }
}
