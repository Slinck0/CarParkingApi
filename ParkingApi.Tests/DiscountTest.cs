using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using V2.Data;
using V2.Models;
using V2.Helpers; // Zorg dat DbContextHelper hier gevonden wordt

namespace ParkingApi.Tests.Handlers;

public class DiscountHandlerTests
{
    private readonly Mock<HttpContext> _mockHttp;

    public DiscountHandlerTests()
    {
        _mockHttp = new Mock<HttpContext>();
    }

    // --- POST DISCOUNT TESTS ---

    [Fact]
    public async Task PostDiscount_ReturnsCreated_WhenDataIsValid()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var request = new CreateDiscountRequest
        {
            Code = "SUMMER2025",
            Percentage = 15,
            ValidUntil = DateTime.Now.AddDays(30)
        };

        // Act
        var result = await DiscountHandler.PostDiscount(_mockHttp.Object, db, request);

        // Assert
        var createdResult = Assert.IsType<Created<DiscountModel>>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal("SUMMER2025", createdResult.Value?.Code);

        // Check of het in de DB staat
        var savedDiscount = await db.Set<DiscountModel>().FirstOrDefaultAsync(d => d.Code == "SUMMER2025");
        Assert.NotNull(savedDiscount);
    }

    [Fact]
    public async Task PostDiscount_ReturnsConflict_WhenCodeAlreadyExists()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        // Voeg alvast een korting toe
        db.Set<DiscountModel>().Add(new DiscountModel { Code = "EXISTING", Percentage = 10, ValidUntil = DateTime.Now.AddDays(1) });
        db.SaveChanges();

        var request = new CreateDiscountRequest
        {
            Code = "EXISTING", // Dezelfde code
            Percentage = 20,
            ValidUntil = DateTime.Now.AddDays(5)
        };

        // Act
        var result = await DiscountHandler.PostDiscount(_mockHttp.Object, db, request);

        // Assert
        var conflictResult = Assert.IsType<Conflict<string>>(result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task PostDiscount_ReturnsBadRequest_WhenCodeIsEmpty()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new CreateDiscountRequest { Code = "", Percentage = 10 };

        var result = await DiscountHandler.PostDiscount(_mockHttp.Object, db, request);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task PostDiscount_ReturnsBadRequest_WhenPercentageIsZeroOrLess()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new CreateDiscountRequest { Code = "TEST", Percentage = 0 };

        var result = await DiscountHandler.PostDiscount(_mockHttp.Object, db, request);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task PostDiscount_ReturnsBadRequest_WhenPercentageIsAbove100()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new CreateDiscountRequest { Code = "TEST", Percentage = 101 };

        var result = await DiscountHandler.PostDiscount(_mockHttp.Object, db, request);

        Assert.IsType<BadRequest<string>>(result);
    }

    // --- GET DISCOUNTS TESTS ---

    [Fact]
    public async Task GetDiscounts_ReturnsListOfDiscounts()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Set<DiscountModel>().Add(new DiscountModel { Code = "A", Percentage = 10 });
        db.Set<DiscountModel>().Add(new DiscountModel { Code = "B", Percentage = 20 });
        db.SaveChanges();

        // Act
        var result = await DiscountHandler.GetDiscounts(_mockHttp.Object, db);

        // Assert
        var okResult = Assert.IsType<Ok<List<DiscountModel>>>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(2, okResult.Value?.Count);
    }

    [Fact]
    public async Task GetDiscounts_ReturnsEmptyList_WhenNoDiscountsExist()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext(); // Lege DB

        // Act
        var result = await DiscountHandler.GetDiscounts(_mockHttp.Object, db);

        // Assert
        var okResult = Assert.IsType<Ok<List<DiscountModel>>>(result);
        Assert.Empty(okResult.Value!);
    }
}