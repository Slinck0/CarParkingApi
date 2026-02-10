using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using V2.Data;
using V2.Models;
using ParkingApi.Tests.Unit;
using V2.Handlers;

namespace ParkingApi.Tests.Unit.Handlers;

public class DiscountHandlerTests
{
    private readonly Mock<HttpContext> _mockHttp;

    public DiscountHandlerTests()
    {
        _mockHttp = new Mock<HttpContext>();
        var mockUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "admin@test.com"),
            new Claim(ClaimTypes.Role, "ADMIN")
        }));
        _mockHttp.Setup(h => h.User).Returns(mockUser);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsCreated_WhenPercentageDiscountIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new CreateDiscountRequest
        {
            Code = "SUMMER25",
            Type = DiscountType.Percentage,
            Percentage = 15,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30)
        };

        var result = await DiscountHandler.CreateDiscount(_mockHttp.Object, db, request);

        var createdResult = Assert.IsType<Created<DiscountModel>>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal("SUMMER25", createdResult.Value?.Code);
        Assert.Equal(DiscountType.Percentage, createdResult.Value?.Type);
        Assert.Equal(15, createdResult.Value?.Percentage);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsCreated_WhenFixedAmountDiscountIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new CreateDiscountRequest
        {
            Code = "FIXED10",
            Type = DiscountType.FixedAmount,
            FixedAmount = 10.50m,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30)
        };

        var result = await DiscountHandler.CreateDiscount(_mockHttp.Object, db, request);

        var createdResult = Assert.IsType<Created<DiscountModel>>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal("FIXED10", createdResult.Value?.Code);
        Assert.Equal(DiscountType.FixedAmount, createdResult.Value?.Type);
        Assert.Equal(10.50m, createdResult.Value?.FixedAmount);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsConflict_WhenCodeAlreadyExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "EXISTING",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var request = new CreateDiscountRequest
        {
            Code = "EXISTING",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(5)
        };

        var result = await DiscountHandler.CreateDiscount(_mockHttp.Object, db, request);

        var conflictResult = Assert.IsType<Conflict<string>>(result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsBadRequest_WhenCodeIsEmpty()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new CreateDiscountRequest
        {
            Code = "",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(1)
        };

        var result = await DiscountHandler.CreateDiscount(_mockHttp.Object, db, request);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsBadRequest_WhenPercentageIsInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new CreateDiscountRequest
        {
            Code = "TEST",
            Type = DiscountType.Percentage,
            Percentage = 150,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(1)
        };

        var result = await DiscountHandler.CreateDiscount(_mockHttp.Object, db, request);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task GetAllDiscounts_ReturnsListOfDiscounts()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Set<DiscountModel>().AddRange(
            new DiscountModel
            {
                Code = "A",
                Type = DiscountType.Percentage,
                Percentage = 10,
                ValidUntil = DateTimeOffset.UtcNow.AddDays(1),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin"
            },
            new DiscountModel
            {
                Code = "B",
                Type = DiscountType.FixedAmount,
                FixedAmount = 5,
                ValidUntil = DateTimeOffset.UtcNow.AddDays(2),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin"
            }
        );
        await db.SaveChangesAsync();

        var result = await DiscountHandler.GetAllDiscounts(db);

        var okResult = Assert.IsType<Ok<List<DiscountResponse>>>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(2, okResult.Value?.Count);
    }

    [Fact]
    public async Task GetDiscountByCode_ReturnsDiscount_WhenCodeExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "TESTCODE",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(5),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var result = await DiscountHandler.GetDiscountByCode("TESTCODE", db);

        var okResult = Assert.IsType<Ok<DiscountDetailResponse>>(result);
        Assert.Equal("TESTCODE", okResult.Value?.Code);
    }

    [Fact]
    public async Task GetDiscountByCode_ReturnsNotFound_WhenCodeDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var result = await DiscountHandler.GetDiscountByCode("NONEXISTENT", db);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task UpdateDiscount_UpdatesFields_WhenDiscountExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var discount = new DiscountModel
        {
            Code = "UPDATE",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(1),
            IsActive = true,
            MaxUsageCount = 10,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        };
        db.Set<DiscountModel>().Add(discount);
        await db.SaveChangesAsync();

        var updateRequest = new UpdateDiscountRequest
        {
            IsActive = false,
            MaxUsageCount = 20
        };

        var result = await DiscountHandler.UpdateDiscount(discount.Id, db, updateRequest);

        var okResult = Assert.IsType<Ok<DiscountModel>>(result);
        Assert.False(okResult.Value?.IsActive);
        Assert.Equal(20, okResult.Value?.MaxUsageCount);
    }

    [Fact]
    public async Task DeactivateDiscount_SetsIsActiveToFalse()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var discount = new DiscountModel
        {
            Code = "DEACTIVATE",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        };
        db.Set<DiscountModel>().Add(discount);
        await db.SaveChangesAsync();

        var result = await DiscountHandler.DeactivateDiscount(discount.Id, db);

        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var updated = await db.Set<DiscountModel>().FindAsync(discount.Id);
        Assert.False(updated?.IsActive);
    }
}
