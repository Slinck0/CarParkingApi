using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using V2.Data;
using V2.Models;
using V2.Handlers; 
using V2.Helpers;  
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ParkingApi.Tests.Unit.Handlers;

public class BillingHandlerTests
{
   
    private Mock<HttpContext> CreateMockHttp(int userId)
    {
        var mockHttp = new Mock<HttpContext>();
        var claims = new List<Claim> 
        { 
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()) 
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        
        mockHttp.Setup(c => c.User).Returns(principal);
        return mockHttp;
    }



    [Fact]
    public async Task GetUpcomingPayments_ReturnsOk_AndFiltersCorrectly()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 10;
        
        db.Users.Add(new UserModel { Id = userId, Username="U", Email="e", Name="N", Password="p", Phone="0", Active=true });


        db.Reservations.Add(new ReservationModel 
        { 
            Id = "res-future", UserId = userId, Cost = 10m, Status = ReservationStatus.confirmed, 
            StartTime = DateTimeOffset.UtcNow.AddDays(2) 
        });
        db.Reservations.Add(new ReservationModel 
        { 
            Id = "res-paid", UserId = userId, Cost = 10m, Status = ReservationStatus.paid 
        });
        db.Reservations.Add(new ReservationModel 
        { 
            Id = "res-cancel", UserId = userId, Cost = 10m, Status = ReservationStatus.cancelled 
        });
        db.ParkingSessions.Add(new ParkingSessionModel 
        { 
            Id = 100, UserId = userId, Cost = 5m, Status = "active", 
            StartTime = DateTime.UtcNow.AddHours(-1) 
        });
        db.ParkingSessions.Add(new ParkingSessionModel 
        { 
            Id = 101, UserId = userId, Cost = 5m, Status = "Paid" 
        });

        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);
        var result = await BillingHandlers.GetUpcomingPayments(mockHttp.Object.User, db, mockHttp.Object);


        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);
        var valueProp = result.GetType().GetProperty("Value");
        var val = valueProp?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val).ToList();

        Assert.Equal(2, list.Count); 
        var firstItem = list[0];
        var typeProp = firstItem.GetType().GetProperty("Type");
        var idProp = firstItem.GetType().GetProperty("Id");
        Assert.Equal("ParkingSession", typeProp?.GetValue(firstItem));
        Assert.Equal(100, idProp?.GetValue(firstItem));
    }

    [Fact]
    public async Task GetUpcomingPayments_ReturnsUnauthorized_WhenNotLoggedIn()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = new Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal()); // Lege user

        var result = await BillingHandlers.GetUpcomingPayments(null!, db, mockHttp.Object);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }
    [Fact]
    public async Task GetBillingHistory_ReturnsOk_WithCombinedHistory()
    {
    
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 50;

        db.Users.Add(new UserModel { Id = userId, Username="U", Email="e", Name="N", Password="p", Phone="0", Active=true });

  
        db.Reservations.Add(new ReservationModel 
        { 
            Id = "res-hist-1", UserId = userId, Cost = 20m, Status = ReservationStatus.paid,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5) 
        });

        db.Reservations.Add(new ReservationModel 
        { 
            Id = "res-future", UserId = userId, Cost = 20m, Status = ReservationStatus.confirmed 
        });

        db.ParkingSessions.Add(new ParkingSessionModel 
        { 
            Id = 500, UserId = userId, Cost = 8m, Status = "Paid", LicensePlate = "AA-BB",
            EndTime = DateTime.UtcNow.AddDays(-1) // Gisteren
        });
        
        db.ParkingSessions.Add(new ParkingSessionModel 
        { 
            Id = 501, UserId = userId, Cost = 8m, Status = "completed", LicensePlate = "XX-YY",
            EndTime = DateTime.UtcNow.AddDays(-2) 
        });

        db.ParkingSessions.Add(new ParkingSessionModel 
        { 
            Id = 502, UserId = userId, Cost = 0m, Status = "active" 
        });

        db.SaveChanges();
        var mockHttp = CreateMockHttp(userId);

        var result = await BillingHandlers.GetBillingHistory(mockHttp.Object, db);

    
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var valueProp = result.GetType().GetProperty("Value");
        var val = valueProp?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<HistoryItemDto>>(val).ToList();

        Assert.Equal(3, list.Count); 

   
        
        Assert.Equal("500", list[0].Id);
        Assert.Equal("501", list[1].Id);
        Assert.Equal("res-hist-1", list[2].Id);
    }

    [Fact]
    public async Task GetBillingHistory_ReturnsUnauthorized_WhenNotLoggedIn()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = new Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal());

        var result = await BillingHandlers.GetBillingHistory(mockHttp.Object, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}
