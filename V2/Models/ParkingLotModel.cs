namespace V2.Models;

public class ParkingLot
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string Address { get; set; } = null!;
    public int Capacity { get; set; }
    public int Reserved { get; set; }
    public decimal Tariff { get; set; }
    public decimal? DayTariff { get; set; }
    public DateOnly CreatedAt { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? Status { get; set; }
    public string? ClosedReason { get; set; }
    public DateOnly? ClosedDate { get; set; }
}
