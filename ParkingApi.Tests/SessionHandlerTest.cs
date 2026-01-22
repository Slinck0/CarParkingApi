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
            Color = "Black" 
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
        var request = new StartStopSessionRequest("ONBEKEND-12");
        
        var result = await SessionHandlers.StartSession(1, db, mockHttp.Object, request);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task StopSession_ReturnsOk_WhenActiveSessionExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        await SeedDatabase(db);
        var mockHttp = CreateMockHttp(_testUserId);

        db.ParkingSessions.Add(new ParkingSessionModel
        {
            Id = 500,
            UserId = _testUserId,
            VehicleId = 10,
            LicensePlate = "AA-BB-99",
            ParkingLotId = 1,
            StartTime = DateTime.UtcNow.AddHours(-2), 
            Status = "active",
            EndTime = null
        });
        await db.SaveChangesAsync();

        var request = new StartStopSessionRequest("AA-BB-99");

        var result = await SessionHandlers.StopSession(1, db, mockHttp.Object, request);

        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var session = await db.ParkingSessions.FindAsync(500);
        Assert.NotNull(session);
        Assert.Equal("completed", session.Status);
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
}