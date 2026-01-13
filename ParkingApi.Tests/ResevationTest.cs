using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using V2.Models;
using V2.Helpers;
using System.Security.Claims;
using System.Collections; 

namespace ParkingApi.Tests.Handlers;

public class ReservationHandlerTests
{
    private readonly Mock<HttpContext> _mockHttp;
    private readonly int _testUserId = 99;

    public ReservationHandlerTests()
    {
        _mockHttp = new Mock<HttpContext>();
        
        // Simuleer een ingelogde gebruiker met ID 99
        var claims = new Claim[] { new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()) };
        var claimsIdentity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        _mockHttp.Setup(c => c.User).Returns(claimsPrincipal);
    }

    // --- CREATE TESTS ---

    [Fact]
    public async Task CreateReservation_ReturnsCreated_WhenDataIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        // Setup Garage met ALLE verplichte velden (Location en Address toegevoegd!)
        db.ParkingLots.Add(new ParkingLotModel { 
            Id = 1, 
            Name = "Test Garage", 
            Tariff = 5.0m, 
            Capacity = 10, 
            CreatedAt = DateOnly.FromDateTime(DateTime.Now), 
            Status = "Open",
            Location = "Rotterdam",      // <--- Was vergeten
            Address = "Coolsingel 1"     // <--- Was vergeten
        });
        db.SaveChanges(); // Nu zou deze regel niet meer moeten crashen

        var request = new ReservationRequest("AA-BB-12", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 1);

        // Act
        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, statusCodeResult.StatusCode); // 201 Created

        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.UserId == _testUserId);
        Assert.NotNull(reservation);
        Assert.True(reservation.Cost > 0);
    }

    [Fact]
    public async Task CreateReservation_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        // Geen garage toegevoegd aan DB!

        var request = new ReservationRequest("AA-BB-12", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 999); // ID 999 bestaat niet

        // Act
        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        // Assert
        Assert.IsType<NotFound<string>>(result); // Verwacht 404
    }

    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenEndDateIsBeforeStartDate()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new ReservationRequest("AA-BB-12", DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(1), 1); // Eind voor start

        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        Assert.IsType<BadRequest<string>>(result); // Verwacht 400
    }

    // --- GET MY RESERVATIONS TESTS ---

    [Fact]
    public async Task GetMyReservations_ReturnsOnlyMyReservations()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        // Reservering van mij (ID 99)
        db.Reservations.Add(new ReservationModel { Id = "mijn-1", UserId = _testUserId, ParkingLotId = 1, Status = ReservationStatus.confirmed, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) });
        // Reservering van iemand anders (ID 888)
        db.Reservations.Add(new ReservationModel { Id = "ander-1", UserId = 888, ParkingLotId = 1, Status = ReservationStatus.confirmed, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) });
        db.SaveChanges();

        // Act
        var result = await ReservationHandlers.GetMyReservations(_mockHttp.Object, db);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        // Check of er maar 1 in de lijst zit
        var valueProperty = result.GetType().GetProperty("Value");
        var list = valueProperty?.GetValue(result) as IEnumerable;
        
        int count = 0;
        foreach (var item in list!) count++;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetMyReservations_ReturnsEmptyList_WhenUserHasNoReservations()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        // Lege DB (of alleen reserveringen van anderen)

        // Act
        var result = await ReservationHandlers.GetMyReservations(_mockHttp.Object, db);

        var valueProperty = result.GetType().GetProperty("Value");
        var list = valueProperty?.GetValue(result) as IEnumerable;
        
        int count = 0;
        foreach (var item in list!) count++;
        Assert.Equal(0, count);
    }

    // --- CANCEL TESTS ---

    [Fact]
    public async Task CancelReservation_ReturnsOk_WhenReservationExistsAndIsYours()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var resId = "res-123";
        // Reservering van mij
        db.Reservations.Add(new ReservationModel { Id = resId, UserId = _testUserId, Status = ReservationStatus.confirmed, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) });
        db.SaveChanges();

        var result = await ReservationHandlers.CancelReservation(resId, db); 

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);
        
        var updatedRes = await db.Reservations.FindAsync(resId);
        Assert.Equal(ReservationStatus.cancelled, updatedRes!.Status);
    }

    [Fact]
    public async Task CancelReservation_ReturnsNotFound_WhenIdDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        // Act: Probeer iets te annuleren wat niet bestaat
        var result = await ReservationHandlers.CancelReservation("niet-bestaand-id", db);

        // Assert
        Assert.IsType<NotFound<string>>(result);
    }
}