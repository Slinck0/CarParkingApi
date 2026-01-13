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
        
        db.ParkingLots.Add(new ParkingLotModel { 
            Id = 1, 
            Name = "Test Garage", 
            Tariff = 5.0m, 
            Capacity = 10, 
            CreatedAt = DateOnly.FromDateTime(DateTime.Now), 
            Status = "Open",
            Location = "Rotterdam",
            Address = "Coolsingel 1"
        });
        db.SaveChanges();

        var request = new ReservationRequest("AA-BB-12", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 1);

        // Act
        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, statusCodeResult.StatusCode);

        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.UserId == _testUserId);
        Assert.NotNull(reservation);
        Assert.True(reservation.Cost > 0);
    }

    [Fact]
    public async Task CreateReservation_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new ReservationRequest("AA-BB-12", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), 999);

        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenEndDateIsBeforeStartDate()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new ReservationRequest("AA-BB-12", DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(1), 1);

        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        Assert.IsType<BadRequest<string>>(result); // Let op: Je handler stuurt een object terug, of string afhankelijk van de validatie
    }

    // --- GET MY RESERVATIONS TESTS ---

    [Fact]
    public async Task GetMyReservations_ReturnsOnlyMyReservations()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Reservations.Add(new ReservationModel { Id = "mijn-1", UserId = _testUserId, ParkingLotId = 1, Status = ReservationStatus.confirmed, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) });
        db.Reservations.Add(new ReservationModel { Id = "ander-1", UserId = 888, ParkingLotId = 1, Status = ReservationStatus.confirmed, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) });
        db.SaveChanges();

        // Act
        var result = await ReservationHandlers.GetMyReservations(_mockHttp.Object, db);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

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
    public async Task CancelReservation_ReturnsOk_WhenReservationExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var resId = "res-123";
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
        
        var result = await ReservationHandlers.CancelReservation("niet-bestaand-id", db);

        Assert.IsType<NotFound<string>>(result);
    }

    // --- UPDATE TESTS (NIEUW) ---

    [Fact]
    public async Task UpdateReservation_ReturnsOk_WhenDataIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        // Arrange: Setup Garage en Bestaande Reservering
        db.ParkingLots.Add(new ParkingLotModel { Id = 1, Name = "Garage A", Tariff = 10.0m, Capacity = 50, Status = "Open", Location = "X", Address = "Y" });
        
        var existingResId = "res-update-1";
        db.Reservations.Add(new ReservationModel { 
            Id = existingResId, 
            UserId = _testUserId, 
            ParkingLotId = 1, 
            StartTime = DateTime.UtcNow, 
            EndTime = DateTime.UtcNow.AddHours(1),
            Cost = 10.0m,
            Status = ReservationStatus.confirmed
        });
        db.SaveChanges();

        // Nieuwe data: 2 uur later, dus hogere kosten
        var updateRequest = new ReservationRequest("NIEUW-KENTEKEN", DateTime.UtcNow.AddHours(5), DateTime.UtcNow.AddHours(7), 1);

        // Act
        var result = await ReservationHandlers.UpdateReservation(existingResId, db, updateRequest, _mockHttp.Object);

        // Assert
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        // Check DB update
        var updatedRes = await db.Reservations.FindAsync(existingResId);
        Assert.NotNull(updatedRes);
        // De starttijd moet geupdate zijn (we checken globaal of het in de buurt ligt van de request)
        Assert.Equal(updateRequest.StartDate.Value.Hour, updatedRes.StartTime.Hour);
        // De kosten moeten opnieuw berekend zijn (2 uur * 10 euro = 20 euro, was 10)
        Assert.True(updatedRes.Cost > 10.0m);
    }

    [Fact]
    public async Task UpdateReservation_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var updateRequest = new ReservationRequest("AA", DateTime.Now, DateTime.Now.AddHours(1), 1);

        // Act
        var result = await ReservationHandlers.UpdateReservation("bestaat-niet", db, updateRequest, _mockHttp.Object);

        // Assert
        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task UpdateReservation_ReturnsNotFound_WhenNewParkingLotDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        // Arrange: Bestaande reservering
        var resId = "res-bad-lot";
        db.Reservations.Add(new ReservationModel { Id = resId, UserId = _testUserId, ParkingLotId = 1, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) });
        db.SaveChanges();

        // Request met ParkingLot ID 999 (bestaat niet)
        var updateRequest = new ReservationRequest("AA", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 999);

        // Act
        var result = await ReservationHandlers.UpdateReservation(resId, db, updateRequest, _mockHttp.Object);

        // Assert
        Assert.IsType<NotFound<ErrorResponse>>(result);
    }

    [Fact]
    public async Task UpdateReservation_ReturnsBadRequest_WhenEndDateBeforeStartDate()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        // Arrange
        var resId = "res-dates";
        db.Reservations.Add(new ReservationModel { Id = resId, UserId = _testUserId, ParkingLotId = 1 });
        db.SaveChanges();

        // Foutieve datum
        var updateRequest = new ReservationRequest("AA", DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(1), 1);

        // Act
        var result = await ReservationHandlers.UpdateReservation(resId, db, updateRequest, _mockHttp.Object);

        // Assert
        // Je handler geeft string terug bij datumfouten in update
        Assert.IsType<BadRequest<ErrorResponse>>(result);
    }

    [Fact]
    public async Task UpdateReservation_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var resId = "res-fields";
        db.Reservations.Add(new ReservationModel { Id = resId, UserId = _testUserId });
        db.SaveChanges();

        // Lege request (geen license plate, geen dates)
        var updateRequest = new ReservationRequest("", null, null, 0);

        // Act
        var result = await ReservationHandlers.UpdateReservation(resId, db, updateRequest, _mockHttp.Object);

        // Assert
        // Je handler geeft een object terug bij missing fields
        Assert.IsType<BadRequest<ErrorResponse>>(result);
    }
}