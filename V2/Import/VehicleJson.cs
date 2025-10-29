namespace ParkingImporter.Import;

public class VehicleRaw
{
    public string id { get; set; } = null!;
    public string user_id { get; set; } = null!;
    public string license_plate { get; set; } = null!;
    public string make { get; set; } = null!;
    public string model { get; set; } = null!;
    public string color { get; set; } = null!;
    public string year { get; set; } = null!;
    public string? created_at { get; set; }
}
