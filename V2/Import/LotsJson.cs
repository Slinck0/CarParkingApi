namespace ParkingImporter.Import;

public class Coordinates { public double lat { get; set; } public double lng { get; set; } }

public class LotRaw
{
    public string id { get; set; } = null!;
    public string name { get; set; } = null!;
    public string location { get; set; } = null!;
    public string address { get; set; } = null!;
    public int capacity { get; set; }
    public int reserved { get; set; }
    public decimal tariff { get; set; }
    public decimal daytariff { get; set; }
    public string created_at { get; set; } = null!;
    public Coordinates? coordinates { get; set; }
    public string? status { get; set; }
    public string? closed_reason { get; set; }
    public string? closed_date { get; set; }
}
