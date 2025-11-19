using ParkingImporter.Data;
using ParkingImporter.Models;
using ParkingApi.Services;
using Microsoft.EntityFrameworkCore;

namespace ParkingApi.Endpoints;

public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapGet("/Health", () => "Parking API is running...");
        app.MapPost("/register", (RegisterUserRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.PhoneNumber) || req.BirthYear <= 0)
            {
                return Results.BadRequest("Bad request:/nUsername, Password, Name, PhoneNumber, Email and BirthYear are required.");
            }
            if (!req.Email.Contains("@") || !req.Email.Contains("."))
            {
                return Results.BadRequest("Bad request:/nInvalid email format.");
            }

            var exist = db.Users.Any(u => u.Username == req.Username || u.Email == req.Email);
            if (exist)
            {
                return Results.Conflict("Bad request:/nUsername or Email already exists.");
            }
            var user = new User
            {
                Username = req.Username,
                Password = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Email = req.Email,
                Name = req.Name,
                Phone = req.PhoneNumber,
                BirthYear = req.BirthYear,
                CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
                Active = true
            };

            db.Users.Add(user);
            db.SaveChanges();
            return Results.Created($"/users/{user.Id}", new { user.Id, user.Username, user.Email });
        }).WithTags("Authentication");

        app.MapPost("/login", async (LoginRequest req, AppDbContext db, TokenService token) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new
                {
                    error = "missing_fields",
                    message = "Bad request:/nUsername and Password are required."
                });
            }
            var user = db.Users.FirstOrDefault(u => u.Username == req.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
            {
                return Results.Unauthorized();
            }

            var jwt = token.CreateToken(user);
            return Results.Ok(new
            {
                token = jwt,
                user = new UserResponse(user.Id, user.Username, user.Role.ToString())
            });
        }).WithTags("Authentication");

        var reservationGroup = app.MapGroup("/reservations").RequireAuthorization().WithTags("Reservations");

        reservationGroup.MapPost("", async (HttpContext http, ReservationRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.LicensePlate) ||
                !req.StartDate.HasValue ||
                !req.EndDate.HasValue ||
                req.ParkingLot <= 0 ||
                req.VehicleId <= 0)
            {
                return Results.BadRequest(new
                {
                    error = "missing_fields",
                    message = "Bad request:/nLicensePlate, StartDate, EndDate, ParkingLot and VehicleId are required."
                });
            }

            var startDate = req.StartDate.Value;
            var endDate = req.EndDate.Value;

            if (endDate <= startDate)
                return Results.BadRequest("Bad request:/nEndDate must be after StartDate.");

            var parkingLot = await db.ParkingLots.FirstOrDefaultAsync(p => p.Id == req.ParkingLot);
            if (parkingLot is null)
                return Results.NotFound("Parking lot not found.");
            int.TryParse(
                http.User?.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value,
                out var userId);
            var (price, _, _) = Helpers.CalculatePrice(parkingLot, startDate, endDate);


            var r = new Reservation
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                ParkingLotId = parkingLot.Id,
                VehicleId = req.VehicleId,
                StartTime = startDate,
                EndTime = endDate,
                CreatedAt = DateTime.UtcNow,
                Status = ReservationStatus.confirmed,
                Cost = price
            };

            db.Reservations.Add(r);
            await db.SaveChangesAsync();

            var response = new
            {
                status = "Success",
                reservation = new
                {
                    licenseplate = req.LicensePlate,
                    startdate = startDate.ToString("yyyy-MM-dd"),
                    enddate = endDate.ToString("yyyy-MM-dd"),
                    parkinglot = req.ParkingLot.ToString()
                }
            };

            return Results.Created($"/reservations/{r.Id}", response);
        });

        reservationGroup.MapGet("/me", async (HttpContext http, AppDbContext db) =>
        {
            var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Results.Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return Results.BadRequest("Invalid user ID in token.");

            var reservations = await db.Reservations
                .Where(r => r.UserId == userId)
                .Select(r => new
                {
                    r.Id,
                    r.ParkingLotId,
                    r.VehicleId,
                    r.StartTime,
                    r.EndTime,
                    Status = r.Status.ToString(),
                    r.Cost
                })
                .ToListAsync();

            return Results.Ok(reservations);
        });

        reservationGroup.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
            if (reservation == null)
            {
                return Results.NotFound("Reservation not found.");
            }

            if (reservation.Status == ReservationStatus.cancelled)
            {
                return Results.BadRequest("Reservation is already cancelled.");
            }

            reservation.Status = ReservationStatus.cancelled;
            await db.SaveChangesAsync();

            return Results.Ok(new { status = "Success", message = "Reservation cancelled successfully." });
        });
        reservationGroup.MapPut("/{id}", async (string id, AppDbContext db, ReservationRequest req) =>
        {
            var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id);
            if (reservation == null)
            {
                return Results.NotFound("Reservation not found.");
            }

            if (string.IsNullOrWhiteSpace(req.LicensePlate) ||
                !req.StartDate.HasValue ||
                !req.EndDate.HasValue ||
                req.ParkingLot <= 0 ||
                req.VehicleId <= 0)
            {
                return Results.BadRequest(new
                {
                    error = "missing_fields",
                    message = "Bad request:/nLicensePlate, StartDate, EndDate, ParkingLot and VehicleId are required."
                });
            }
            var startDate = req.StartDate.Value;
            var endDate = req.EndDate.Value;
            if (endDate <= startDate)
            {
                return Results.BadRequest("Bad request:/nEndDate must be after StartDate.");
            }
            var parkingLot = await db.ParkingLots.FirstOrDefaultAsync(p => p.Id == req.ParkingLot);
            if (parkingLot is null)
                return Results.NotFound("Parking lot not found.");

            var (price, _, _) = Helpers.CalculatePrice(parkingLot, startDate, endDate);

            reservation.ParkingLotId = parkingLot.Id;
            reservation.VehicleId = req.VehicleId;
            reservation.StartTime = startDate;
            reservation.EndTime = endDate;
            reservation.Cost = price;


            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                status = "Success",
                reservation = new
                {
                    licenseplate = req.LicensePlate,
                    startdate = startDate.ToString("yyyy-MM-dd"),
                    enddate = endDate.ToString("yyyy-MM-dd"),
                    parkinglot = req.ParkingLot.ToString()
                }


            });

        });
        app.MapPost("/vehicles", async (Vehicle vehicle, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(vehicle.LicensePlate))
                return Results.BadRequest("License plate is required.");

            if (await db.Vehicles.AnyAsync(v => v.LicensePlate == vehicle.LicensePlate))
                return Results.Conflict("A vehicle with this license plate already exists.");

            db.Vehicles.Add(vehicle);
            await db.SaveChangesAsync();

            return Results.Created($"/vehicles/{vehicle.Id}", vehicle);
        })
        .WithName("CreateVehicle")
        .WithTags("Vehicles");

        app.MapPost("/parkinglots/{id}/sessions/start", async (int id, AppDbContext db, HttpContext http, StartStopSessionRequest req) =>
        {
            var parkingLot = await db.ParkingLots.FindAsync(id);
            if (parkingLot == null)
            {
                return Results.NotFound("Parking lot not found.");
            }
            int.TryParse(
                http.User?.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value,
                out var userId);
            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == req.LicensePlate && v.UserId == userId);
            if (vehicle == null)
            {
                return Results.NotFound("Vehicle not found.");
            }

            var session = new ParkingSessions
            {
                UserId = userId,
                VehicleId = vehicle.Id,
                StartTime = DateTime.UtcNow,
                LicensePlate = req.LicensePlate
            };
            db.ParkingSessions.Add(session);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = $"Session started for vehicle {req.LicensePlate} at parking lot {parkingLot.Name}."
            });
        }).RequireAuthorization().WithTags("Sessions");

        app.MapPost("/parkinglots/{id}/sessions/stop", async (int id, AppDbContext db, HttpContext http, StartStopSessionRequest req) =>
        {
            var parkingLot = await db.ParkingLots.FindAsync(id);
            if (parkingLot == null)
            {
                return Results.NotFound("Parking lot not found.");
            }
            int.TryParse(
                http.User?.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value,
                out var userId);

            var vehicle = await db.Vehicles
                .FirstOrDefaultAsync(v => v.LicensePlate == req.LicensePlate && v.UserId == userId);
            if (vehicle == null)
            {
                return Results.NotFound("Vehicle not found.");
            }
            var session = await db.ParkingSessions
                .Where(s => s.VehicleId == vehicle.Id && s.EndTime == null)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return Results.NotFound("Active parking session not found for this vehicle.");
            }
            session.EndTime = DateTime.UtcNow;
            var duration = session.EndTime.Value - session.StartTime;
            var hours = Math.Ceiling(duration.TotalHours);
            var (price, _, _) = Helpers.CalculatePrice(parkingLot, session.StartTime, session.EndTime.Value);
            var Cost = (decimal)price;
            session.Cost = Cost;

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = $"Session stopped for vehicle {req.LicensePlate} at parking lot {parkingLot.Name}."
            });
        }).RequireAuthorization().WithTags("Sessions");

        app.MapPost("/parking-lots", async (ParkingLotCreate req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) ||
                req.Capacity <= 0 ||
                string.IsNullOrWhiteSpace(req.Location) ||
                string.IsNullOrWhiteSpace(req.Address) ||
                req.Tariff <= 0 ||
                req.DayTariff == null ||
                req.Lat == 0 ||
                req.Lng == 0)
            {
                return Results.BadRequest("Invalid parking lot data.");
            }

            var parkingLot = new ParkingLot
            {
                Name = req.Name,
                Capacity = req.Capacity,
                Location = req.Location,
                Address = req.Address,
                Tariff = req.Tariff,
                DayTariff = req.DayTariff,
                Lat = req.Lat,
                Lng = req.Lng,
                Reserved = 0,
                Status = req.Status,
                ClosedReason = req.ClosedReason,
                ClosedDate = req.ClosedDate,
                CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            db.ParkingLots.Add(parkingLot);
            await db.SaveChangesAsync();

            return Results.Created($"/parking-lots/{parkingLot.Id}", new
            {
                message = "Parking lot created successfully.",
                parkingLotId = parkingLot.Id,
                parkingLotName = parkingLot.Name,
                parkingLotAddress = parkingLot.Address
            });

        }).RequireAuthorization("ADMIN").WithTags("ParkingLots");





    }
}
    

