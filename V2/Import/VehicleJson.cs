using System.Text.Json.Serialization;
 
namespace ParkingImporter.Import;
 
public class VehicleRaw
{
    public string? id { get; set; }
    public string? user_id { get; set; } = null!;
    public string license_plate { get; set; } = null!;
    public string? make { get; set; }
    public string? model { get; set; }
    public string? color { get; set; }
 
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? year { get; set; }
    public string? created_at { get; set; }
}
 
 