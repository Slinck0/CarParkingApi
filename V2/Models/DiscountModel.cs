public class DiscountModel
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public decimal Percentage { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
}

public record CreateDiscountRequest
{
    public string Code { get; set; } = null!;
    public decimal Percentage { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
}