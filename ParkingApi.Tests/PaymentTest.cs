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
using System.Reflection;

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

        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

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

        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

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
            Id = userId,
            Username = "KortingJager",
            Email = "d@d.nl",
            Name = "Jager Naam",
            Password = "Wachtwoord123",
            Phone = "0611223344",
            CreatedAt = DateOnly.FromDateTime(DateTime.Now),
            Active = true
        });

        db.Reservations.Add(new ReservationModel { Id = "res-disc", UserId = userId, Cost = 100.0m, Status = ReservationStatus.confirmed });

        db.Discounts.Add(new DiscountModel
        {
            Id = 1,
            Code = "SUMMER20",
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(1)
        });
        db.SaveChanges();

        var req = new CreatePaymentRequest
        {
            ReservationId = "res-disc",
            Method = "Paypal",
            DiscountCode = "SUMMER20"
        };

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

        var createdResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var payment = await db.Payments.FirstOrDefaultAsync();
        Assert.Equal(80.0m, payment!.Amount);
        Assert.Equal(100.0m, payment.TAmount);
    }

    [Fact]
    public async Task GetPayments_ReturnsOk_WithList_WhenUserHasPayments()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 100;

        db.Users.Add(new UserModel { Id = userId, Username = "Klant", Email = "a", Name = "b", Password = "p", Phone = "06", Active = true });
        db.Reservations.Add(new ReservationModel { Id = "res-1", UserId = userId, Cost = 25.00m, Status = ReservationStatus.paid });

        db.Payments.Add(new Payment
        {
            Transaction = "trans-123",
            ReservationId = "res-1",
            Amount = 25.00m,
            Status = PaymentStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            Method = "Ideal",
            Initiator = "System",
            Hash = "dummy-hash-123",
            Issuer = "TestBank",
            Bank = "ING"
        });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandler.GetPayments(mockHttp.Object, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var resultType = result.GetType();
        var valueProp = resultType.GetProperty("Value");
        var val = valueProp?.GetValue(result);

        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val);
        Assert.Single(list);

        var item = list.First();
        var transProp = item.GetType().GetProperty("Transaction");
        var amountProp = item.GetType().GetProperty("Amount");

        Assert.Equal("trans-123", transProp?.GetValue(item));
        Assert.Equal(25.00m, amountProp?.GetValue(item));
    }

    [Fact]
    public async Task GetPayments_ReturnsEmptyList_WhenUserHasNoPayments()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 200; 

        var otherUser = 999;
        db.Reservations.Add(new ReservationModel { Id = "res-other", UserId = otherUser, Cost = 10 });
        
        db.Payments.Add(new Payment 
        { 
            Transaction = "trans-other", 
            ReservationId = "res-other", 
            Amount = 10,
            Status = PaymentStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            Method = "Card",
            Initiator = "OtherUser",
            Hash = "hash-other",
            Issuer = "BankB",
            Bank = "Rabo"
        });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandler.GetPayments(mockHttp.Object, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var val = result.GetType().GetProperty("Value")?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val);
        
        Assert.Empty(list); 
    }

    [Fact]
    public async Task GetPayments_DoesNotReturnPayments_WithoutReservation()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 300;

        db.Payments.Add(new Payment
        {
            Transaction = "orphan-payment",
            ReservationId = "res-deleted", 
            Amount = 50.00m,
            Status = PaymentStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            Method = "Cash",
            Initiator = "System",
            Hash = "hash-orphan",
            Issuer = "N/A",
            Bank = "N/A"
        });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandler.GetPayments(mockHttp.Object, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var val = result.GetType().GetProperty("Value")?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val);
        
        Assert.Empty(list);
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

        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

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

        var result = await PaymentHandler.CreatePayment(mockHttp.Object, db, req);

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task UpcomingPayments_ReturnsCorrectList()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 50;

        db.Reservations.Add(new ReservationModel
        { 
            Id = "res-unpaid", UserId = userId, Cost = 20m, Status = ReservationStatus.confirmed, 
            StartTime = DateTime.UtcNow.AddDays(1) 
        });
        
        db.Reservations.Add(new ReservationModel
        { 
            Id = "res-paid", UserId = userId, Cost = 20m, Status = ReservationStatus.paid 
        });

        db.ParkingSessions.Add(new ParkingSessionModel
        { 
            Id = 500, UserId = userId, Cost = 5.50m, Status = "Ended", 
            StartTime = DateTime.UtcNow.AddHours(-5), LicensePlate = "AA"
        });

        db.SaveChanges();
        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandler.UpcomingPayments(mockHttp.Object, db);

        var okResult = Assert.IsType<Ok<List<object>>>(result);
        var list = okResult.Value;
        
        Assert.Equal(2, list!.Count); 
    }
}