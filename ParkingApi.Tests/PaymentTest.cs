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
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ParkingApi.Tests.Handlers;

public class PaymentHandlerTests
{
    private Mock<HttpContext> CreateMockHttp(int userId)
    {
        var mockHttp = new Mock<HttpContext>();
        var claims = new Claim[] 
        { 
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()) 
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        mockHttp.Setup(c => c.User).Returns(principal);
        return mockHttp;
    }

    // --- CREATE PAYMENT TESTS ---

    [Fact]
    public async Task CreatePayment_ReturnsCreated_WhenPayingReservation_Success()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 10;
        
        db.Users.Add(new UserModel 
        { 
            Id = userId, Username = "Klant", Email = "k@k.nl", Name = "Klant Naam", Password = "Wachtwoord123", Phone = "0612345678", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true 
        });

        db.Reservations.Add(new ReservationModel 
        { 
            Id = "res-1", UserId = userId, Cost = 50.0m, Status = ReservationStatus.confirmed 
        });
        db.SaveChanges();

        var req = new CreatePaymentRequest { ReservationId = "res-1", Method = "CreditCard" };
        var mockHttp = CreateMockHttp(userId);

        // Act
        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

        // Assert
        // We checken nu of het resultaat 'IStatusCodeHttpResult' is en of de status 201 is.
        // Dit werkt ook voor anonieme types.
        var createdResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var payment = await db.Payments.FirstOrDefaultAsync();
        Assert.NotNull(payment);
        Assert.Equal(50.0m, payment.Amount);
        
        var res = await db.Reservations.FindAsync("res-1");
        Assert.Equal(ReservationStatus.paid, res!.Status); 
    }

    [Fact]
    public async Task CreatePayment_ReturnsCreated_WhenPayingParkingSession_Success()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 20;

        db.Users.Add(new UserModel
        { 
            Id = userId, Username = "Parkeerder", Email="p@p.nl", Name = "Parkeerder Naam", Password = "Wachtwoord123", Phone = "0687654321", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true 
        });

        db.ParkingSessions.Add(new ParkingSessionModel
        { 
            Id = 100, UserId = userId, Cost = 12.50m, Status = "Ended", StartTime = DateTime.Now.AddHours(-2), LicensePlate = "AA-BB-CC"
        });
        db.SaveChanges();

        var req = new CreatePaymentRequest { ParkingSessionId = "100", Method = "Ideal" };
        var mockHttp = CreateMockHttp(userId);

        // Act
        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

        // Assert
        var createdResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        
        var session = await db.ParkingSessions.FindAsync(100);
        Assert.Equal("Paid", session!.Status);
    }

    [Fact]
    public async Task CreatePayment_CalculatesDiscount_Correctly()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 30;

        db.Users.Add(new UserModel
        { 
            Id = userId, Username = "KortingJager", Email="d@d.nl", Name = "Jager Naam", Password = "Wachtwoord123", Phone = "0611223344", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true 
        });

        db.Reservations.Add(new ReservationModel { Id = "res-disc", UserId = userId, Cost = 100.0m, Status = ReservationStatus.confirmed });
        
        db.Discounts.Add(new DiscountModel 
        { 
            Id = 1, Code = "SUMMER20", Percentage = 20, ValidUntil = DateTimeOffset.UtcNow.AddDays(1) 
        });
        db.SaveChanges();

        var req = new CreatePaymentRequest 
        { 
            ReservationId = "res-disc", Method = "Paypal", DiscountCode = "SUMMER20"
        };

        var mockHttp = CreateMockHttp(userId);

        // Act
        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

        // Assert
        var createdResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var payment = await db.Payments.FirstOrDefaultAsync();
        Assert.Equal(80.0m, payment!.Amount); 
        Assert.Equal(100.0m, payment.TAmount); 
    }

    [Fact]
    public async Task CreatePayment_ReturnsBadRequest_WhenAlreadyPaid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 10;

        db.Users.Add(new UserModel { Id = userId, Username="U", Email="e", Name = "Test", Password = "pw", Phone = "06", CreatedAt=DateOnly.MinValue, Active=true });
        db.Reservations.Add(new ReservationModel { Id = "res-paid", UserId = userId, Cost = 10m, Status = ReservationStatus.paid });
        db.SaveChanges();

        var req = new CreatePaymentRequest { ReservationId = "res-paid", Method = "Card" };
        var mockHttp = CreateMockHttp(userId);

        // Act
        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

        // Assert
        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task CreatePayment_ReturnsForbidden_WhenPayingForOtherUser()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var myId = 10;
        var otherId = 99;

        db.Users.Add(new UserModel { Id = myId, Username="Me", Email="e", Name = "Ik", Password = "pw", Phone = "06", CreatedAt=DateOnly.MinValue, Active=true });
        db.Reservations.Add(new ReservationModel { Id = "res-other", UserId = otherId, Cost = 10m });
        db.SaveChanges();

        var req = new CreatePaymentRequest { ReservationId = "res-other", Method = "Card" };
        var mockHttp = CreateMockHttp(myId);

        // Act
        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    // --- UPCOMING PAYMENTS TESTS ---

    [Fact]
    public async Task UpcomingPayments_ReturnsCorrectList()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 50;

        // 1. Onbetaalde reservering
        db.Reservations.Add(new ReservationModel
        { 
            Id = "res-unpaid", UserId = userId, Cost = 20m, Status = ReservationStatus.confirmed, 
            StartTime = DateTime.UtcNow.AddDays(1) 
        });
        
        // 2. Betaalde reservering (mag NIET in de lijst)
        db.Reservations.Add(new ReservationModel
        { 
            Id = "res-paid", UserId = userId, Cost = 20m, Status = ReservationStatus.paid 
        });

        // 3. Onbetaalde parkeersessie
        db.ParkingSessions.Add(new ParkingSessionModel
        { 
            Id = 500, UserId = userId, Cost = 5.50m, Status = "Ended", 
            StartTime = DateTime.UtcNow.AddHours(-5), LicensePlate = "AA"
        });

        db.SaveChanges();
        var mockHttp = CreateMockHttp(userId);

        // Act
        var result = await PaymentHandler.UpcomingPayments(mockHttp.Object, db);

        // Assert
        var okResult = Assert.IsType<Ok<List<object>>>(result);
        var list = okResult.Value;
        
        Assert.Equal(2, list!.Count); 
    }
}