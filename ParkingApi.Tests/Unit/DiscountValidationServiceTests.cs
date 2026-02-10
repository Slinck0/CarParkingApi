using Xunit;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Services;
using ParkingApi.Tests.Unit;

namespace ParkingApi.Tests.Services;

public class DiscountValidationServiceTests
{
    [Fact]
    public async Task ValidateDiscountCodeAsync_ReturnsValid_WhenDiscountIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        // Add a user
        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        // Add a valid discount
        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "VALID20",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            CurrentUsageCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, discount) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "VALID20", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.True(isValid);
        Assert.Equal("Discount code is valid.", message);
        Assert.NotNull(discount);
        Assert.Equal("VALID20", discount.Code);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_ReturnsInvalid_WhenDiscountNotFound()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });
        await db.SaveChangesAsync();

        var (isValid, message, discount) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "NONEXISTENT", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.False(isValid);
        Assert.Contains("not found or inactive", message);
        Assert.Null(discount);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_ReturnsInvalid_WhenDiscountExpired()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "EXPIRED",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(-1),  // Expired
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, discount) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "EXPIRED", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.False(isValid);
        Assert.Contains("expired", message);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_ReturnsInvalid_WhenUsageLimitReached()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "LIMITED",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            MaxUsageCount = 5,
            CurrentUsageCount = 5,  // Limit reached
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, discount) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "LIMITED", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.False(isValid);
        Assert.Contains("maximum usage limit", message);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_ReturnsInvalid_WhenUserRestrictionNotMet()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });
        db.Users.Add(new UserModel { Id = 2, Name = "Other User", Email = "other@test.com", Password = "hash", Phone = "0987654321", Username = "otheruser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "USERSPECIFIC",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            UserId = 2,  // Restricted to user 2
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, discount) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "USERSPECIFIC", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.False(isValid);
        Assert.Contains("not available for your account", message);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_ReturnsInvalid_WhenParkingLotRestrictionNotMet()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "LOTSPECIFIC",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            ParkingLotId = 5,  // Restricted to lot 5
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, discount) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "LOTSPECIFIC", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.False(isValid);
        Assert.Contains("not valid for this parking lot", message);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_ReturnsInvalid_WhenDurationTooShort()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "LONGSTAY",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            MinReservationDuration = TimeSpan.FromHours(4),  // Min 4 hours
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, discount) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "LONGSTAY", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.False(isValid);
        Assert.Contains("Minimum reservation duration", message);
    }

    [Fact]
    public void CalculateDiscountAmount_CalculatesPercentageCorrectly()
    {
        var discount = new DiscountModel
        {
            Type = DiscountType.Percentage,
            Percentage = 20
        };

        var amount = DiscountValidationService.CalculateDiscountAmount(discount, 100m);

        Assert.Equal(20m, amount);
    }

    [Fact]
    public void CalculateDiscountAmount_CalculatesFixedAmountCorrectly()
    {
        var discount = new DiscountModel
        {
            Type = DiscountType.FixedAmount,
            FixedAmount = 15m
        };

        var amount = DiscountValidationService.CalculateDiscountAmount(discount, 100m);

        Assert.Equal(15m, amount);
    }

    [Fact]
    public void CalculateDiscountAmount_DoesNotExceedOriginalAmount()
    {
        var discount = new DiscountModel
        {
            Type = DiscountType.FixedAmount,
            FixedAmount = 150m
        };

        var amount = DiscountValidationService.CalculateDiscountAmount(discount, 100m);

        Assert.Equal(100m, amount);  // Should cap at original amount
    }

    [Fact]
    public async Task RecordDiscountUsageAsync_CreatesUsageRecordAndIncrementsCount()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var discount = new DiscountModel
        {
            Code = "TEST",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            CurrentUsageCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        };
        db.Set<DiscountModel>().Add(discount);
        await db.SaveChangesAsync();

        await DiscountValidationService.RecordDiscountUsageAsync(
            discount, "RES123", 1, 100m, 20m, 80m, db);

        var usage = await db.Set<DiscountUsageModel>().FirstOrDefaultAsync();
        Assert.NotNull(usage);
        Assert.Equal("RES123", usage.ReservationId);
        Assert.Equal(20m, usage.DiscountAmount);

        var updatedDiscount = await db.Set<DiscountModel>().FindAsync(discount.Id);
        Assert.Equal(1, updatedDiscount?.CurrentUsageCount);
    }
}
