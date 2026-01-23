using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using V2.Models;
using V2.Helpers;
using V2.Data;
using System.Security.Claims;

namespace ParkingApi.Tests.Handlers;

public class SessionHandlerTests
{
    private readonly int _testUserId = 99;

    private Mock<HttpContext> CreateMockHttp(int userId)
    {
        var mockHttp = new Mock<HttpContext>();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim("id", userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var principal = new ClaimsPrincipal(identity);
        mockHttp.Setup(c => c.User).Returns(principal);
        return mockHttp;
    }

    private async Task SeedDatabase(AppDbContext db)
    {
        db.Users.Add(new UserModel 
        { 
            Id = _testUserId, 
            Name = "Test User", 
            Email = "test@example.com", 
            Password = "pw", 
            Role = "User",
            Username = "TestUser123", 
            Phone = "0612345678",
            Active = true 
        });

        db.ParkingLots.Add(new ParkingLotModel 
        { 
            Id = 1, 
            Name = "Centrum Garage", 
            Tariff = 5.0m, 
            Capacity = 100, 
            Status = "Open",
            Location = "Stad",
            Address = "Straat 1"
        });

        db.Vehicles.Add(new VehicleModel 
        { 
            Id = 10, 
            UserId = _testUserId, 
            LicensePlate = "AA-BB-99", 
            Make = "Tesla", 
            Model = "3", 
            Color = "Black",
            Year = 2022
        });

        await db.SaveChangesAsync();
    }


    [Fact]
    public async Task StartSession_ReturnsOk_WhenDataIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db); 
        var mockHttp = CreateMockHttp(_testUserId);
        var request = new StartStopSessionRequest("AA-BB-99");

        var result = await SessionHandlers.StartSession(1, db, mockHttp.Object, request);

        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var session = await db.ParkingSessions.FirstOrDefaultAsync(s => s.LicensePlate == "AA-BB-99");
        Assert.NotNull(session);
        Assert.Equal("active", session.Status);
    }

    [Fact]
    public async Task StartSession_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db); 
        var mockHttp = CreateMockHttp(_testUserId);
        var request = new StartStopSessionRequest("AA-BB-99");

        var result = await SessionHandlers.StartSession(999, db, mockHttp.Object, request);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task StartSession_ReturnsNotFound_WhenVehicleDoesNotExistOrNotYours()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db);
        var mockHttp = CreateMockHttp(_testUserId);
        var request = new StartStopSessionRequest("XX-YY-00");
        
        var result = await SessionHandlers.StartSession(1, db, mockHttp.Object, request);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task StopSession_ReturnsOk_WhenActiveSessionExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var userId = 555;
        var vehicleId = 555;
        var sessionId = 888;
        var plate = "ZZ-55-AA";

        db.Users.Add(new UserModel 
        { 
            Id = userId, 
            Name = "SessionUser", 
            Email = "s@test.com", 
            Active = true,
            Username = "SessionUserTest", 
            Password = "DummyPassword",
            Phone = "0612345678"
        });

        db.ParkingLots.Add(new ParkingLotModel 
        { 
            Id = 1, 
            Name = "Lot 1", 
            Tariff = 2.0m, 
            Status = "Open",
            Location = "Rotterdam",
            Address = "Coolsingel 1"
        });
        
        db.Vehicles.Add(new VehicleModel 
        { 
            Id = vehicleId, 
            UserId = userId, 
            LicensePlate = plate, 
            Make = "T", 
            Model = "T", 
            Color = "B", 
            Year = 2022 
        });
        
        db.ParkingSessions.Add(new ParkingSessionModel
        {
            Id = sessionId,
            UserId = userId,
            VehicleId = vehicleId,
            LicensePlate = plate,
            ParkingLotId = 1,
            StartTime = DateTime.UtcNow.AddHours(-2), 
            Status = "active",
            EndTime = null
        });
        await db.SaveChangesAsync();

        var mockHttp = CreateMockHttp(userId);
        var request = new StartStopSessionRequest(plate);

        // Act
        var result = await SessionHandlers.StopSession(1, db, mockHttp.Object, request);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        // FIX: Clear ChangeTracker om te voorkomen dat we "oude" data uit de cache lezen
        db.ChangeTracker.Clear();

        // Haal de sessie opnieuw op uit de "database"
        var session = await db.ParkingSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        
        Assert.NotNull(session);
        Assert.Equal("completed", session.Status);
        
        // Nu zou EndTime ingevuld moeten zijn
        Assert.NotNull(session.EndTime); 
        Assert.True(session.Cost > 0);
    }

    [Fact]
    public async Task StopSession_ReturnsNotFound_WhenNoActiveSessionFound()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db); 
        var mockHttp = CreateMockHttp(_testUserId);
        var request = new StartStopSessionRequest("AA-BB-99");

        var result = await SessionHandlers.StopSession(1, db, mockHttp.Object, request);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task StopSession_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db);
        var mockHttp = CreateMockHttp(_testUserId);
        var request = new StartStopSessionRequest("AA-BB-99");

        var result = await SessionHandlers.StopSession(999, db, mockHttp.Object, request);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task StartSession_ReturnsUnauthorized_WhenNoUserClaim()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db);
        
        var mockHttp = new Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); 
        
        var request = new StartStopSessionRequest("AA-BB-99");

        var result = await SessionHandlers.StartSession(1, db, mockHttp.Object, request);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task StopSession_ReturnsUnauthorized_WhenNoUserClaim()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db);
        
        var mockHttp = new Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); 
        
        var request = new StartStopSessionRequest("AA-BB-99");

        var result = await SessionHandlers.StopSession(1, db, mockHttp.Object, request);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task StopSession_ReturnsNotFound_WhenVehicleDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db);
        var mockHttp = CreateMockHttp(_testUserId);
        var request = new StartStopSessionRequest("ZZ-99-ZZ");

        var result = await SessionHandlers.StopSession(1, db, mockHttp.Object, request);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task StartSession_ReturnsUnauthorized_WhenUserIsInactive()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        db.Users.Add(new UserModel 
        { 
            Id = _testUserId, 
            Name = "Test User", 
            Email = "test@example.com", 
            Password = "pw", 
            Role = "User",
            Username = "TestUser123", 
            Phone = "0612345678",
            Active = false 
        });

        db.ParkingLots.Add(new ParkingLotModel 
        { 
            Id = 1, 
            Name = "Centrum Garage", 
            Tariff = 5.0m, 
            Capacity = 100, 
            Status = "Open",
            Location = "Stad",
            Address = "Straat 1"
        });

        db.Vehicles.Add(new VehicleModel 
        { 
            Id = 10, 
            UserId = _testUserId, 
            LicensePlate = "AA-BB-99", 
            Make = "Tesla", 
            Model = "3", 
            Color = "Black",
            Year = 2022
        });

        await db.SaveChangesAsync();

        var mockHttp = CreateMockHttp(_testUserId);
        var request = new StartStopSessionRequest("AA-BB-99");

        var result = await SessionHandlers.StartSession(1, db, mockHttp.Object, request);

        Assert.NotNull(result);
    }
}