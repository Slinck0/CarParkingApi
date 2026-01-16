using System.Text.Json.Serialization;

namespace V2.Models;

public class VehicleModel
{
    [JsonIgnore]
    public int Id { get; set; }
    [JsonIgnore]
    public int UserId { get; set; }
    public int? OrganizationId { get; set; }
    public string LicensePlate { get; set; } = null!;
    public string Make { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int Year { get; set; }
    [JsonIgnore]
    public DateOnly CreatedAt { get; set; }
}
