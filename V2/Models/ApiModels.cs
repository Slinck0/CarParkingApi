namespace V2.Models;

record RegisterUserRequest(string Username, string Password, string Name, string PhoneNumber, string Email, int BirthYear);
record LoginRequest(string Username, string Password);
record UserResponse(int Id, string Username, string Role);
record StartStopSessionRequest(string LicensePlate);
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
    int ParkingLot,
    int VehicleId
);

public record UpdateProfileRequest(
    string Name,
    string Email,
    string PhoneNumber,
    int BirthYear
);