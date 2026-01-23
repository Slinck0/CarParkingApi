using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using V2.Models;
using V2.Helpers;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ParkingApi.Tests.Handlers;

public class PaymentHandlersTests
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
            Id = userId,
            Username = "Klant",
            Email = "k@k.nl",
            Name = "Klant Naam",
            Password = "Wachtwoord123",
            Phone = "0612345678",
            CreatedAt = DateOnly.FromDateTime(DateTime.Now),
            Active = true
        });

        db.Reservations.Add(new ReservationModel
        {
            Id = "res-1",
            UserId = userId,
            Cost = 50.0m,
            Status = ReservationStatus.confirmed
        });
        db.SaveChanges();

        var req = new CreatePaymentRequest("res-1", "CreditCard");
        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandlers.CreatePayment(mockHttp.Object, db, req);

        var createdResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var payment = await db.Payments.Cast<PaymentModel>().FirstOrDefaultAsync();
        Assert.NotNull(payment);
        Assert.Equal(50.0m, payment!.Amount);
        Assert.Equal("res-1", payment.ReservationId);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
    }

    [Fact]
    public async Task CreatePayment_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 10;

        db.Users.Add(new UserModel
        {
            Id = userId,
            Username = "U",
            Email = "e",
            Name = "n",
            Password = "pw",
            Phone = "06",
            CreatedAt = DateOnly.MinValue,
            Active = true
        });
        db.SaveChanges();

        var req = new CreatePaymentRequest("missing", "Card");
        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandlers.CreatePayment(mockHttp.Object, db, req);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task CreatePayment_ReturnsForbidden_WhenPayingForOtherUser()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var myId = 10;
        var otherId = 99;

        db.Users.Add(new UserModel { Id = myId, Username = "Me", Email = "e", Name = "Ik", Password = "pw", Phone = "06", CreatedAt = DateOnly.MinValue, Active = true });
        db.Reservations.Add(new ReservationModel { Id = "res-other", UserId = otherId, Cost = 10m, Status = ReservationStatus.confirmed });
        db.SaveChanges();

        var req = new CreatePaymentRequest("res-other", "Card");
        var mockHttp = CreateMockHttp(myId);

        var result = await PaymentHandlers.CreatePayment(mockHttp.Object, db, req);

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task GetUserPayments_ReturnsOk_WithList_WhenUserHasCompletedPayments()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 100;

        db.Users.Add(new UserModel { Id = userId, Username = "Klant", Email = "a", Name = "b", Password = "p", Phone = "06", Active = true });
        db.Reservations.Add(new ReservationModel { Id = "res-1", UserId = userId, Cost = 25.00m, Status = ReservationStatus.paid });

        db.Payments.Add(new PaymentModel
        {
            Transaction = "trans-123",
            ReservationId = "res-1",
            Amount = 25.00m,
            TAmount = 25.00m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = "Ideal",
            Initiator = "System",
            Hash = "dummy-hash-123",
            Issuer = "TestBank",
            Bank = "ING"
        });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandlers.GetUserPayments(mockHttp.Object, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var val = result.GetType().GetProperty("Value")?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val);

        Assert.Single(list);
        var item = list.First();

        var transProp = item.GetType().GetProperty("Transaction");
        var amountProp = item.GetType().GetProperty("Amount");

        Assert.Equal("trans-123", transProp?.GetValue(item));
        Assert.Equal(25.00m, amountProp?.GetValue(item));
    }

    [Fact]
    public async Task GetUserNonCompletedPayments_ReturnsOk_WithList_WhenUserHasPendingPayments()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 200;

        db.Users.Add(new UserModel { Id = userId, Username = "Klant", Email = "a", Name = "b", Password = "p", Phone = "06", Active = true });
        db.Reservations.Add(new ReservationModel { Id = "res-pending", UserId = userId, Cost = 10.00m, Status = ReservationStatus.confirmed });

        db.Payments.Add(new PaymentModel
        {
            Transaction = "trans-pending",
            ReservationId = "res-pending",
            Amount = 10.00m,
            TAmount = 10.00m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = "Card",
            Initiator = "System",
            Hash = "hash",
            Issuer = "Bank",
            Bank = "ING"
        });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandlers.GetUserNonCompletedPayments(mockHttp.Object, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var val = result.GetType().GetProperty("Value")?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val);

        Assert.Single(list);
    }

    [Fact]
    public async Task GetUserPayments_DoesNotReturnPayments_WithoutReservation()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 300;

        db.Payments.Add(new PaymentModel
        {
            Transaction = "orphan-payment",
            ReservationId = "res-deleted",
            Amount = 50.00m,
            TAmount = 50.00m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = "Cash",
            Initiator = "System",
            Hash = "hash-orphan",
            Issuer = "N/A",
            Bank = "N/A"
        });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandlers.GetUserPayments(mockHttp.Object, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var val = result.GetType().GetProperty("Value")?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val);

        Assert.Empty(list);
    }

    [Fact]
    public async Task AdminCancelUserPayment_ReturnsNotFound_WhenPaymentMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var result = await PaymentHandlers.AdminCancelUserPayment(db, "missing-tx");

        Assert.IsType<NotFound<string>>(result);
    }



    [Fact]
    public async Task AdminCancelUserPayment_SetsPendingToFailed()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var oldCompletedAt = DateTimeOffset.UtcNow.AddDays(-1);

        db.Payments.Add(new PaymentModel
        {
            Transaction = "tx-pending",
            ReservationId = "res-1",
            Amount = 10m,
            TAmount = 10m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            CompletedAt = oldCompletedAt,
            Method = "Card",
            Hash = "h"
        });
        db.SaveChanges();

        var result = await PaymentHandlers.AdminCancelUserPayment(db, "tx-pending");

        var ok = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var updated = await db.Payments.Cast<PaymentModel>().FirstAsync(p => p.Transaction == "tx-pending");
        Assert.Equal(PaymentStatus.Failed, updated.Status);
        Assert.True(updated.CompletedAt > oldCompletedAt);
    }

    [Fact]
    public async Task AdminCancelUserPayment_SetsCompletedToRefunded()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var oldCompletedAt = DateTimeOffset.UtcNow.AddDays(-1);

        db.Payments.Add(new PaymentModel
        {
            Transaction = "tx-completed",
            ReservationId = "res-2",
            Amount = 25m,
            TAmount = 25m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            CompletedAt = oldCompletedAt,
            Method = "Ideal",
            Hash = "h2"
        });
        db.SaveChanges();

        var result = await PaymentHandlers.AdminCancelUserPayment(db, "tx-completed");

        var ok = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var updated = await db.Payments.Cast<PaymentModel>().FirstAsync(p => p.Transaction == "tx-completed");
        Assert.Equal(PaymentStatus.Refunded, updated.Status);
        Assert.True(updated.CompletedAt > oldCompletedAt);
    }

    [Fact]
    public async Task AdminCancelUserPayment_ReturnsBadRequest_WhenAlreadyFailedOrRefunded()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Payments.Add(new PaymentModel
        {
            Transaction = "tx-failed",
            ReservationId = "res-3",
            Amount = 5m,
            TAmount = 5m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = "Cash",
            Hash = "h3"
        });
        db.SaveChanges();

        var result = await PaymentHandlers.AdminCancelUserPayment(db, "tx-failed");

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task AdminUpdatePayment_ReturnsNotFound_WhenPaymentMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var req = new PaymentHandlers.AdminUpdatePaymentRequest(Amount: 20m, Method: "Ideal", Status: PaymentStatus.Completed);
        var result = await PaymentHandlers.AdminUpdatePayment(db, "missing", req);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task AdminUpdatePayment_UpdatesAmount_Method_AndStatus()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var oldCompletedAt = DateTimeOffset.UtcNow.AddDays(-3);

        db.Payments.Add(new PaymentModel
        {
            Transaction = "tx-update",
            ReservationId = "res-4",
            Amount = 10m,
            TAmount = 10m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-4),
            CompletedAt = oldCompletedAt,
            Method = "Card",
            Hash = "h4"
        });
        db.SaveChanges();

        var req = new PaymentHandlers.AdminUpdatePaymentRequest(Amount: 99m, Method: "Ideal", Status: PaymentStatus.Completed);
        var result = await PaymentHandlers.AdminUpdatePayment(db, "tx-update", req);

        var ok = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var updated = await db.Payments.Cast<PaymentModel>().FirstAsync(p => p.Transaction == "tx-update");
        Assert.Equal(99m, updated.Amount);
        Assert.Equal(99m, updated.TAmount);
        Assert.Equal("Ideal", updated.Method);
        Assert.Equal(PaymentStatus.Completed, updated.Status);
        Assert.True(updated.CompletedAt > oldCompletedAt);
    }

    [Fact]
    public async Task AdminUpdatePayment_ReturnsBadRequest_WhenAmountInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Payments.Add(new PaymentModel
        {
            Transaction = "tx-badamount",
            ReservationId = "res-5",
            Amount = 10m,
            TAmount = 10m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = "Card",
            Hash = "h5"
        });
        db.SaveChanges();

        var req = new PaymentHandlers.AdminUpdatePaymentRequest(Amount: 0m, Method: null, Status: null);
        var result = await PaymentHandlers.AdminUpdatePayment(db, "tx-badamount", req);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task AdminCancelUserPayment_ReturnsBadRequest_WhenAlreadyRefunded()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Payments.Add(new PaymentModel
        {
            Transaction = "tx-refunded",
            ReservationId = "res-r",
            Amount = 5m,
            TAmount = 5m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Refunded,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = "Ideal",
            Hash = "h-ref"
        });
        db.SaveChanges();

        var result = await PaymentHandlers.AdminCancelUserPayment(db, "tx-refunded");

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task CreatePayment_ReturnsBadRequest_WhenDataIsInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 10;

        db.Users.Add(new UserModel
        {
            Id = userId,
            Username = "Klant",
            Email = "k@k.nl",
            Name = "Klant Naam",
            Password = "Wachtwoord123",
            Phone = "0612345678",
            CreatedAt = DateOnly.FromDateTime(DateTime.Now),
            Active = true
        });
        db.SaveChanges();

        // Missing ReservationId and Method
        var req = new CreatePaymentRequest("", "");
        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandlers.CreatePayment(mockHttp.Object, db, req);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task CreatePayment_ReturnsUnauthorized_WhenNoUserClaim()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Reservations.Add(new ReservationModel
        {
            Id = "res-1",
            UserId = 10,
            Cost = 50.0m,
            Status = ReservationStatus.confirmed
        });
        db.SaveChanges();

        var mockHttp = new Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // No claims

        var req = new CreatePaymentRequest("res-1", "CreditCard");

        var result = await PaymentHandlers.CreatePayment(mockHttp.Object, db, req);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task GetUserPayments_ReturnsUnauthorized_WhenNoUserClaim()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var mockHttp = new Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // No claims

        var result = await PaymentHandlers.GetUserPayments(mockHttp.Object, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task GetUserNonCompletedPayments_ReturnsUnauthorized_WhenNoUserClaim()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var mockHttp = new Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // No claims

        var result = await PaymentHandlers.GetUserNonCompletedPayments(mockHttp.Object, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task GetUserPayments_ReturnsEmptyList_WhenUserHasNoPayments()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 400;

        db.Users.Add(new UserModel { Id = userId, Username = "NoPayments", Email = "a", Name = "b", Password = "p", Phone = "06", Active = true });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandlers.GetUserPayments(mockHttp.Object, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var val = result.GetType().GetProperty("Value")?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val);

        Assert.Empty(list);
    }

    [Fact]
    public async Task GetUserNonCompletedPayments_ReturnsEmptyList_WhenUserHasNoNonCompletedPayments()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var userId = 500;

        db.Users.Add(new UserModel { Id = userId, Username = "AllComplete", Email = "a", Name = "b", Password = "p", Phone = "06", Active = true });
        db.Reservations.Add(new ReservationModel { Id = "res-complete", UserId = userId, Cost = 10.00m, Status = ReservationStatus.paid });

        db.Payments.Add(new PaymentModel
        {
            Transaction = "trans-complete",
            ReservationId = "res-complete",
            Amount = 10.00m,
            TAmount = 10.00m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = "Card",
            Initiator = "System",
            Hash = "hash",
            Issuer = "Bank",
            Bank = "ING"
        });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(userId);

        var result = await PaymentHandlers.GetUserNonCompletedPayments(mockHttp.Object, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var val = result.GetType().GetProperty("Value")?.GetValue(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(val);

        Assert.Empty(list);
    }

    [Fact]
    public async Task AdminUpdatePayment_PartialUpdate_OnlyUpdatesSpecifiedFields()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Payments.Add(new PaymentModel
        {
            Transaction = "tx-partial",
            ReservationId = "res-p",
            Amount = 50m,
            TAmount = 50m,
            TDate = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = "Card",
            Hash = "h-partial"
        });
        db.SaveChanges();

        // Only update method, not amount or status
        var req = new PaymentHandlers.AdminUpdatePaymentRequest(Amount: null, Method: "PayPal", Status: null);
        var result = await PaymentHandlers.AdminUpdatePayment(db, "tx-partial", req);

        var ok = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var updated = await db.Payments.Cast<PaymentModel>().FirstAsync(p => p.Transaction == "tx-partial");
        Assert.Equal(50m, updated.Amount); // Unchanged
        Assert.Equal("PayPal", updated.Method); // Changed
        Assert.Equal(PaymentStatus.Pending, updated.Status); // Unchanged
    }
}
