using Xunit;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Services;
using ParkingApi.Tests.Unit;

namespace ParkingApi.Tests.EdgeCases;

public class DiscountEdgeCaseTests
{
    [Fact]
    public async Task ValidateDiscountCodeAsync_IsCaseInsensitive()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "TESTCODE",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        // Test lowercase
        var (isValid1, _, _) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "testcode", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);
        Assert.True(isValid1);

        // Test mixed case
        var (isValid2, _, _) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "TestCode", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);
        Assert.True(isValid2);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_HandlesWhitespace()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "TRIMTEST",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        // Test with leading/trailing whitespace
        var (isValid, _, _) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "  TRIMTEST  ", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_RejectsInactiveDiscount()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "INACTIVE",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = false,  // Deactivated
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, _) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "INACTIVE", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.False(isValid);
        Assert.Contains("not found or inactive", message);
    }

    [Fact]
    public void CalculateDiscountAmount_HandlesZeroAmount()
    {
        var discount = new DiscountModel
        {
            Type = DiscountType.Percentage,
            Percentage = 20
        };

        var amount = DiscountValidationService.CalculateDiscountAmount(discount, 0m);

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void CalculateDiscountAmount_RoundsCorrectly()
    {
        var discount = new DiscountModel
        {
            Type = DiscountType.Percentage,
            Percentage = 33.33m
        };

        var amount = DiscountValidationService.CalculateDiscountAmount(discount, 100m);

        // Should round to 2 decimal places
        Assert.Equal(33.33m, amount);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_HandlesOrganizationRestriction()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org1", Address = "Address1" });
        db.Organizations.Add(new OrganizationModel { Id = 2, Name = "Org2", Address = "Address2" });

        db.Users.Add(new UserModel
        {
            Id = 1,
            Name = "Test User",
            Email = "test@test.com",
            Password = "hash",
            Phone = "1234567890",
            Username = "testuser",
            OrganizationId = 1
        });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "ORG2ONLY",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            OrganizationId = 2,  // Restricted to org 2
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, _) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "ORG2ONLY", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(2), db);

        Assert.False(isValid);
        Assert.Contains("specific organizations", message);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_HandlesMaxDurationRestriction()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "SHORTSTAY",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            MaxReservationDuration = TimeSpan.FromHours(2),  // Max 2 hours
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var (isValid, message, _) = await DiscountValidationService.ValidateDiscountCodeAsync(
            "SHORTSTAY", 1, 1, DateTime.UtcNow, DateTime.UtcNow.AddHours(5), db);

        Assert.False(isValid);
        Assert.Contains("Maximum reservation duration", message);
    }

    [Fact]
    public void CalculateDiscountAmount_HandlesVerySmallPercentages()
    {
        var discount = new DiscountModel
        {
            Type = DiscountType.Percentage,
            Percentage = 0.01m  // 0.01%
        };

        var amount = DiscountValidationService.CalculateDiscountAmount(discount, 1000m);

        Assert.Equal(0.10m, amount);
    }

    [Fact]
    public async Task RecordDiscountUsageAsync_HandlesMultipleUsages()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var discount = new DiscountModel
        {
            Code = "MULTI",
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

        // Record first usage
        await DiscountValidationService.RecordDiscountUsageAsync(
            discount, "RES1", 1, 100m, 20m, 80m, db);

        // Record second usage
        await DiscountValidationService.RecordDiscountUsageAsync(
            discount, "RES2", 2, 150m, 30m, 120m, db);

        var usages = await db.Set<DiscountUsageModel>().ToListAsync();
        Assert.Equal(2, usages.Count);

        var updatedDiscount = await db.Set<DiscountModel>().FindAsync(discount.Id);
        Assert.Equal(2, updatedDiscount?.CurrentUsageCount);
    }
}
