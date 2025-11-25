using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Services;

// als je claims leest


namespace V2.Api;

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
        });

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
        });

        var reservationGroup = app.MapGroup("/reservations").RequireAuthorization();

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
                Status = ReservationStatus.pending,
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
        app.MapPost("/vehicles", async (HttpContext http, Vehicle vehicle, AppDbContext db) =>
        {
            var nextID = db.Vehicles.Any() ? await db.Vehicles.MaxAsync(v => v.Id) + 1 : 1;
            vehicle.Id = nextID;
            var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

            vehicle.UserId = Convert.ToInt32(userIdClaim);

            vehicle.CreatedAt = DateOnly.FromDateTime(DateTime.Now);

            if (string.IsNullOrWhiteSpace(vehicle.LicensePlate))
                return Results.BadRequest("License plate is required.");

            if (string.IsNullOrWhiteSpace(vehicle.Model))
                return Results.BadRequest("Model is required.");

            if (string.IsNullOrWhiteSpace(vehicle.Color))
                return Results.BadRequest("Color is required.");

            if (string.IsNullOrWhiteSpace(vehicle.Make))
                return Results.BadRequest("Make is required.");

            if (await db.Vehicles.AnyAsync(v => v.LicensePlate == vehicle.LicensePlate))
                return Results.Conflict("A vehicle with this license plate already exists.");

            db.Vehicles.Add(vehicle);
            await db.SaveChangesAsync();

            return Results.Created($"/vehicles/{vehicle.Id}", vehicle);
        })
        .WithName("CreateVehicle");

        app.MapGet("/vehicles", async (HttpContext http, AppDbContext db) =>
        {
            var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Results.Unauthorized();
            }

            int userId = Convert.ToInt32(userIdClaim);

            var vehicles = await db.Vehicles
                .Where(v => v.UserId == userId)
                .ToListAsync();

            return Results.Ok(vehicles);
        });

        app.MapPut("/vehicles/{id}", async (int id, HttpContext http, Vehicle updatedVehicle, AppDbContext db) =>
        {
            var vehicle = await db.Vehicles.FindAsync(id);
            if (vehicle == null)
                return Results.NotFound("Vehicle not found.");

            var userIdClaim = http.User?.Claims
            .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || vehicle.UserId != Convert.ToInt32(userIdClaim))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(updatedVehicle.LicensePlate))
                return Results.BadRequest("License plate is required.");
            if (string.IsNullOrWhiteSpace(updatedVehicle.Model))
                return Results.BadRequest("Model is required.");
            if (string.IsNullOrWhiteSpace(updatedVehicle.Color))
                return Results.BadRequest("Color is required.");
            if (string.IsNullOrWhiteSpace(updatedVehicle.Make))
                return Results.BadRequest("Make is required.");

            if (await db.Vehicles.AnyAsync(v => v.LicensePlate == updatedVehicle.LicensePlate && v.Id != id))
                return Results.Conflict("A vehicle with this license plate already exists.");

            vehicle.LicensePlate = updatedVehicle.LicensePlate;
            vehicle.Model = updatedVehicle.Model;
            vehicle.Color = updatedVehicle.Color;
            vehicle.Make = updatedVehicle.Make;

            await db.SaveChangesAsync();

            return Results.Ok(vehicle);
        });
        app.MapDelete("vehicles/{id}", async (int id, HttpContext http, AppDbContext db) =>
        {
            var vehicle = await db.Vehicles.FindAsync(id);
            if (vehicle == null)
                return Results.NotFound("Vehicle not found.");

            var userIdClaim = http.User?.Claims
            .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || vehicle.UserId != Convert.ToInt32(userIdClaim))
            {
                return Results.Unauthorized();
            }

            db.Vehicles.Remove(vehicle);
            await db.SaveChangesAsync();

            return Results.Ok(new { status = "Success", message = "Vehicle deleted successfully." });
        });

        // GET /profile
        app.MapGet("/profile", async (HttpContext http, AppDbContext db) =>
        {
            var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Results.Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return Results.BadRequest("Invalid user ID in token.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return Results.NotFound("User not found.");

            var profile = new
            {
                user.Id,
                user.Username,
                user.Name,
                user.Email,
                user.Phone,
                user.Role,
                user.BirthYear,
                user.CreatedAt,
                user.Active
            };

            return Results.Ok(profile);
        })
        .RequireAuthorization();

        // PUT /profile 
        app.MapPut("/profile", async (HttpContext http, AppDbContext db, UpdateProfileRequest req) =>
        {
            // Read the userId from claim
            var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Results.Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return Results.BadRequest("Invalid user ID in token.");

            if (string.IsNullOrWhiteSpace(req.Name) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.PhoneNumber) ||
                req.BirthYear <= 0)
            {
                return Results.BadRequest("Name, Email, PhoneNumber and BirthYear are required.");
            }

            if (!req.Email.Contains("@") || !req.Email.Contains("."))
            {
                return Results.BadRequest("Invalid email format.");
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return Results.NotFound("User not found.");

            // Check if new email is already used by another user
            var emailExists = await db.Users
                .AnyAsync(u => u.Email == req.Email && u.Id != user.Id);

            if (emailExists)
            {
                return Results.Conflict("Email is already in use by another account.");
            }

            user.Name = req.Name;
            user.Email = req.Email;
            user.Phone = req.PhoneNumber;
            user.BirthYear = req.BirthYear;

            await db.SaveChangesAsync();

            var profile = new
            {
                user.Id,
                user.Username,
                user.Name,
                user.Email,
                user.Phone,
                user.Role,
                user.BirthYear,
                user.CreatedAt,
                user.Active
            };

            return Results.Ok(profile);
        })
        .RequireAuthorization();

        // DELETE /profile 
        app.MapDelete("/profile", async (HttpContext http, AppDbContext db) =>
        {
            // Read userId from claims
            var userIdClaim = http.User?.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("/nameidentifier"))?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Results.Unauthorized();

            if (!int.TryParse(userIdClaim, out int userId))
                return Results.BadRequest("Invalid user ID in token.");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return Results.NotFound("User not found.");

            // Delete the user account
            db.Users.Remove(user);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                status = "Success",
                message = "User account deleted successfully."
            });
        })
        .RequireAuthorization();
    }

}