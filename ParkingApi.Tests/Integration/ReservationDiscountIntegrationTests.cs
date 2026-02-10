using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using V2.Data;
using V2.Models;
using Microsoft.Extensions.DependencyInjection;
using ParkingApi.Tests.Unit;

namespace ParkingApi.Tests.Integration;

public class ReservationDiscountIntegrationTests : IntegrationTestBase
{
    private readonly Mock<HttpContext> _mockHttp;

    public ReservationDiscountIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
        _mockHttp = new Mock<HttpContext>();
        var mockUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "test@test.com")
        }));
        _mockHttp.Setup(h => h.User).Returns(mockUser);
    }

    [Fact]
    public async Task CreateReservation_AppliesDiscount_WhenValidDiscountCodeProvided()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Setup user, vehicle, parking lot
        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 1, LicensePlate = "ABC123", Make = "Toyota", Model = "Camry", Color = "Blue", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 1,
            Name = "Test Lot",
            Location = "Test Location",
            Address = "Test Address",
            Tariff = 5m,
            DayTariff = 50m,
            Capacity = 100,
            Reserved = 0,
            Status = "open",
            Lat = 0,
            Lng = 0,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        // Add valid discount
        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "SAVE20",
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            CurrentUsageCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var request = new ReservationRequest(
            "ABC123",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            1,
            "SAVE20"
        );

        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        var createdResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        // Verify reservation was created with discount
        var reservation = await db.Reservations.FirstOrDefaultAsync();
        Assert.NotNull(reservation);
        Assert.Equal("SAVE20", reservation.DiscountCode);
        Assert.True(reservation.DiscountAmount > 0);
        Assert.True(reservation.Cost < reservation.OriginalCost);

        // Verify discount usage was recorded
        var usage = await db.Set<DiscountUsageModel>().FirstOrDefaultAsync();
        Assert.NotNull(usage);
        Assert.Equal(reservation.Id, usage.ReservationId);

        // Verify discount usage count was incremented
        var discount = await db.Set<DiscountModel>().FirstOrDefaultAsync(d => d.Code == "SAVE20");
        Assert.Equal(1, discount?.CurrentUsageCount);
    }

    [Fact]
    public async Task CreateReservation_ReturnsError_WhenInvalidDiscountCodeProvided()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 1, LicensePlate = "ABC123", Make = "Toyota", Model = "Camry", Color = "Blue", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 1,
            Name = "Test Lot",
            Location = "Test Location",
            Address = "Test Address",
            Tariff = 5m,
            DayTariff = 50m,
            Capacity = 100,
            Reserved = 0,
            Status = "open",
            Lat = 0,
            Lng = 0,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        await db.SaveChangesAsync();

        var request = new ReservationRequest(
            "ABC123",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            1,
            "INVALID"
        );

        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        var badRequestResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WorksWithoutDiscount_WhenNoDiscountCodeProvided()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 1, LicensePlate = "ABC123", Make = "Toyota", Model = "Camry", Color = "Blue", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 1,
            Name = "Test Lot",
            Location = "Test Location",
            Address = "Test Address",
            Tariff = 5m,
            DayTariff = 50m,
            Capacity = 100,
            Reserved = 0,
            Status = "open",
            Lat = 0,
            Lng = 0,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        await db.SaveChangesAsync();

        var request = new ReservationRequest(
            "ABC123",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            1,
            "INVALID"  // Mistake in original code handled as null or ignored? Ah, in original: null. Wait, original had: null. I should use null.
        );
        // Wait, original code:
        // var request = new ReservationRequest(..., null);
        
        // I should correct it.
        var request2 = new ReservationRequest(
            "ABC123",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            1,
            null
        );

        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request2, db);

        var createdResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var reservation = await db.Reservations.FirstOrDefaultAsync();
        Assert.NotNull(reservation);
        Assert.Null(reservation.DiscountCode);
        Assert.Equal(0, reservation.DiscountAmount);
        Assert.Equal(reservation.OriginalCost, reservation.Cost);
    }

    [Fact]
    public async Task CreateReservation_AppliesFixedAmountDiscount_Correctly()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 1, LicensePlate = "ABC123", Make = "Toyota", Model = "Camry", Color = "Blue", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 1,
            Name = "Test Lot",
            Location = "Test Location",
            Address = "Test Address",
            Tariff = 5m,
            DayTariff = 50m,
            Capacity = 100,
            Reserved = 0,
            Status = "open",
            Lat = 0,
            Lng = 0,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "FIXED5",
            Type = DiscountType.FixedAmount,
            FixedAmount = 5m,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            CurrentUsageCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var request = new ReservationRequest(
            "ABC123",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            1,
            "FIXED5"
        );

        var result = await ReservationHandlers.CreateReservation(_mockHttp.Object, request, db);

        var createdResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        var reservation = await db.Reservations.FirstOrDefaultAsync();
        Assert.NotNull(reservation);
        Assert.Equal(5m, reservation.DiscountAmount);
    }

    [Fact]
    public async Task UpdateReservation_AppliesDiscount_WhenValidDiscountCodeProvided()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Users.Add(new UserModel { Id = 1, Name = "Test User", Email = "test@test.com", Password = "hash", Phone = "1234567890", Username = "testuser" });
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 1, LicensePlate = "ABC123", Make = "Toyota", Model = "Camry", Color = "Blue", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow) });
        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 1,
            Name = "Test Lot",
            Location = "Test Location",
            Address = "Test Address",
            Tariff = 5m,
            DayTariff = 50m,
            Capacity = 100,
            Reserved = 0,
            Status = "open",
            Lat = 0,
            Lng = 0,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        var reservation = new ReservationModel
        {
            Id = "RES123",
            UserId = 1,
            ParkingLotId = 1,
            VehicleId = 1,
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            Status = ReservationStatus.confirmed,
            Cost = 10m,
            OriginalCost = 10m,
            CreatedAt = DateTime.UtcNow
        };
        db.Reservations.Add(reservation);

        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = "UPDATE10",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            CurrentUsageCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin"
        });
        await db.SaveChangesAsync();

        var updateRequest = new ReservationRequest(
            "ABC123",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(4),
            1,
            "UPDATE10"
        );

        var result = await ReservationHandlers.UpdateReservation("RES123", db, updateRequest, _mockHttp.Object);

        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var updated = await db.Reservations.FindAsync("RES123");
        Assert.NotNull(updated);
        Assert.Equal("UPDATE10", updated.DiscountCode);
        Assert.True(updated.DiscountAmount > 0);
    }
}
