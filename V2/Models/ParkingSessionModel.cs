namespace V2.Models;
public class ParkingSessionModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public string? LicensePlate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal? Cost { get; set; }

    public int ParkingLotId { get; set; }

    public string? Status { get; set; }
}