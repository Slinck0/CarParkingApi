namespace ParkingImporter.Import;

public class UserRaw
{
    public string id { get; set; } = null!;
    public string username { get; set; } = null!;
    public string password { get; set; } = null!;
    public string name { get; set; } = null!;
    public string email { get; set; } = null!;
    public string phone { get; set; } = null!;
    public string? role { get; set; }
    public string? created_at { get; set; }   // bv. "2025-05-22"
    public string birth_year { get; set; } = null; // kan string zijn
    public string? active { get; set; }       // "true"/"false"/"1"/"0"
}
