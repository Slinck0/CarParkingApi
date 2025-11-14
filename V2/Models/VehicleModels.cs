namespace ParkingImporter.Models;

public class Vehicle
{
    public int Id { get; set; } 
    public int UserId { get; set; }
    public string LicensePlate { get; set; } = null!;
    public string Make { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int Year { get; set; }
    public DateOnly CreatedAt { get; set; }
}
