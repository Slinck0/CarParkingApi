record RegisterUserRequest(string Username, string Password,string Name, string PhoneNumber, string Email, int BirthYear);
record LoginRequest(string Username, string Password);
record UserResponse(int Id, string Username, string Role);
public record ReservationRequest(
    string LicensePlate,
    DateTime? StartDate,
    DateTime? EndDate,
    int ParkingLot,
    int VehicleId
);
