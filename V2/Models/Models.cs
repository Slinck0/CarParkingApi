using System.Security.Cryptography.X509Certificates;

public record RegisterUserRequest(string Username, string Password,string Name, string PhoneNumber, string Email, int BirthYear);
public record LoginRequest(string Username, string Password);
public record UserResponse(int Id, string Username, string Role);
public record StartStopSessionRequest(string LicensePlate);

public record ParkingLotCreate(
    string Name,
    string Location,
    string Address,
    int Capacity,
    int Reserved,
    decimal Tariff,
    decimal? DayTariff,
    double Lat,
    double Lng,
    string? Status,
    string? ClosedReason,
    DateOnly? ClosedDate
);

public record ReservationRequest(
    string LicensePlate,
    DateTime? StartDate,
    DateTime? EndDate,
    int ParkingLot
);

public record UpdateProfileRequest(
    string Name,
    string Email,
    string PhoneNumber,
    int BirthYear
);

public record VehicleRequest(
    int UserId,
    int Id,
    DateTime CreatedAt,
    string LicensePlate,
    string Make,
    string Model,
    int Year,
    string Color
);

public record CreateAdminRequest(
    int UserId
);
    public class HistoryItemDto
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
    }
