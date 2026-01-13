using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using V2.Data;
using V2.Models;
using V2.Handlers;
using ParkingApi.Tests.Helpers;

using System.Security.Claims;

namespace ParkingApi.Tests.Handlers;

public class ReservationHandlerTests
{
    private readonly Mock<HttpContext> _mockHttp;
    private readonly int _testUserId = 99;

    public ReservationHandlerTests()
    {
        _mockHttp = new Mock<HttpContext>();
        
        var claims = new Claim[] { new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()) };
        var claimsIdentity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        _mockHttp.Setup(c => c.User).Returns(claimsPrincipal);
    }

    [Fact]
    public async Task CreateReservation_ReturnsCreated_WhenDataIsValid()
    {
        
        using var db = DbContextHelper.GetInMemoryDbContext();

        var parkingLot = new ParkingLotModel
        { 
            Id = 1, 
            Name = "Centrum Garage", 
            Tariff = 5.0m,
            Capacity = 100,
            Location = "Rotterdam",
            Address = "Coolsingel 1",
            CreatedAt = DateOnly.FromDateTime(DateTime.Now),
            Status = "Open",
            DayTariff = 20.0m,
            Lat = 0, Lng = 0, Reserved = 0
        };
        db.ParkingLots.Add(parkingLot);
        db.SaveChanges();

        var startTime = DateTime.UtcNow.AddHours(1);
        var endTime = startTime.AddHours(2);
        
        var request = new ReservationRequest(
            "AA-BB-12", 
            startTime,
            endTime,
            1       
        );

        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, statusCodeResult.StatusCode);

        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.UserId == _testUserId);
        Assert.NotNull(reservation);
        Assert.Equal(1, reservation.ParkingLotId);
        Assert.Equal(50, reservation.VehicleId);
        Assert.Equal("confirmed", reservation.Status.ToString().ToLower()); 
     
        Assert.True(reservation.Cost > 0); 
    }

  
    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenEndDateIsBeforeStartDate()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new ReservationRequest(
            "AA-BB-12",
            DateTime.UtcNow.AddHours(2), 
            DateTime.UtcNow.AddHours(1), 
            1
        );

   
        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

      
        Assert.IsType<BadRequest<string>>(result);
    }


    [Fact]
    public async Task GetMyReservations_ReturnsOnlyMyReservations()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();


        db.Reservations.Add(new ReservationModel { Id = "mijn-reservering", UserId = _testUserId, ParkingLotId = 1, Status = ReservationStatus.confirmed, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) });

        db.Reservations.Add(new ReservationModel { Id = "andere-reservering", UserId = 888, ParkingLotId = 1, Status = ReservationStatus.confirmed, StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) });

        db.SaveChanges();

        // Act
        var result = await ReservationHandlers.GetMyReservations(_mockHttp.Object, db);

     
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);
     
        var valueProperty = result.GetType().GetProperty("Value");
        Assert.NotNull(valueProperty);
        var listValue = valueProperty.GetValue(result);
        
      
        var list = listValue as System.Collections.IEnumerable;
        Assert.NotNull(list);
        
        var count = 0;
        foreach (var item in list) count++;

        Assert.Equal(1, count); 
    }


    [Fact]
    public async Task CancelReservation_ReturnsOk_WhenReservationExists()
    {

        using var db = DbContextHelper.GetInMemoryDbContext();
        var resId = "res-123";


        db.Reservations.Add(new ReservationModel
        {
            Id = resId,
            UserId = _testUserId,
            Status = ReservationStatus.confirmed,
            StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1)
        });
        db.SaveChanges();

        var result = await ReservationHandlers.CancelReservation(resId, db);

     
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);
        
       
        var updatedRes = await db.Reservations.FindAsync(resId);
        Assert.Equal(ReservationStatus.cancelled, updatedRes.Status);
    }
}