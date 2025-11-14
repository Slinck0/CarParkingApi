namespace ParkingImporter.Models;


public enum ReservationStatus { pending, confirmed, cancelled }

public class Reservation
{
    public string Id { get; set; } = null!;
    public int UserId { get; set; }
    public int ParkingLotId { get; set; }
    public int VehicleId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ReservationStatus Status { get; set; }
    public decimal Cost { get; set; }
}
