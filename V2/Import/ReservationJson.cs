namespace ParkingImporter.Import;

public class ReservationRaw
{
    public string id { get; set; } = null!;
    public string user_id { get; set; } = null!;
    public string parking_lot_id { get; set; } = null!;
    public string vehicle_id { get; set; } = null!;
    public string start_time { get; set; } = null!;
    public string end_time { get; set; } = null!;
    public string created_at { get; set; } = null!;
    public string status { get; set; } = null!;
    public decimal cost { get; set; }
}
