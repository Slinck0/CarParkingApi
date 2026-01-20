using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using V2.Models;
using V2.Handlers;
using V2.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace ParkingApi.Tests.Handlers;

public class VehicleHandlerTests
{

    private Mock<HttpContext> CreateMockHttp(int userId)
    {
        var mockHttp = new Mock<HttpContext>();
        
        var claims = new List<Claim> 
        { 
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim("id", userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var principal = new ClaimsPrincipal(identity);

        mockHttp.Setup(c => c.User).Returns(principal);
        return mockHttp;
    }


    [Fact]
    public async Task CreateVehicle_ReturnsCreated_WhenDataIsValid()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = CreateMockHttp(99); 

        var vehicle = new VehicleModel 
        { 
            LicensePlate = "XYZ-999", Make = "Tesla", Model = "Model 3", Color = "Zwart" 
        };

        // Act
        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, vehicle, db);

        // Assert
        var createdResult = Assert.IsType<Created<VehicleModel>>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal(99, createdResult.Value?.UserId);
    }

    [Fact]
    public async Task CreateVehicle_ReturnsUnauthorized_WhenUserNotLoggedIn()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = new Mock<HttpContext>(); 
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));

        var vehicle = new VehicleModel{ LicensePlate = "AA", Make = "B", Model = "C", Color = "D" };

        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, vehicle, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task CreateVehicle_ReturnsConflict_WhenLicensePlateExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, LicensePlate = "ABC-123", Make = "F", Model = "F", Color = "B", UserId = 1, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(123);
        var request = new VehicleModel { LicensePlate = "ABC-123", Make = "F", Model = "Focus", Color = "Rood" };

        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, request, db);

        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public async Task CreateVehicle_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = CreateMockHttp(99);

        // Test 1: Geen kenteken
        var v1 = new VehicleModel { LicensePlate = "", Make = "A", Model = "B", Color = "C" };
        var r1 = await VehicleHandlers.CreateVehicle(mockHttp.Object, v1, db);
        Assert.IsType<BadRequest<string>>(r1);

        // Test 2: Geen Merk
        var v2 = new VehicleModel { LicensePlate = "AA", Make = "", Model = "B", Color = "C" };
        var r2 = await VehicleHandlers.CreateVehicle(mockHttp.Object, v2, db);
        Assert.IsType<BadRequest<string>>(r2);
    }



    [Fact]
    public async Task GetMyVehicles_ReturnsOnlyMyVehicles()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 10, LicensePlate = "MIJN-AUTO", Make="A", Model="A", Color="A", CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.Vehicles.Add(new VehicleModel { Id = 2, UserId = 20, LicensePlate = "ANDERE-AUTO", Make="B", Model="B", Color="B", CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(10); 

        var result = await VehicleHandlers.GetMyVehicles(mockHttp.Object, db);

        var okResult = Assert.IsType<Ok<List<VehicleModel>>>(result);
        var vehicles = Assert.IsType<List<VehicleModel>>(okResult.Value);
        Assert.Single(vehicles); 
        Assert.Equal("MIJN-AUTO", vehicles[0].LicensePlate);
    }


    [Fact]
    public async Task UpdateVehicle_ReturnsOk_WhenUpdatingOwnVehicle()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "OLD-1", Make = "Old", Model = "Old", Color = "Old", CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(50); 
        var updateReq = new VehicleModel { LicensePlate = "NEW-1", Make = "New", Model = "New", Color = "New" };

        var result = await VehicleHandlers.UpdateVehicle(1, mockHttp.Object, updateReq, db);

        Assert.IsType<Ok<VehicleModel>>(result);
        var updated = await db.Vehicles.FindAsync(1);
        Assert.Equal("NEW-1", updated!.LicensePlate);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsUnauthorized_WhenUpdatingOthersVehicle()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "ABC", Make="A", Model="A", Color="A", CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(99); // Hacker
        var updateReq = new VehicleModel { LicensePlate = "HACKED", Make = "A", Model = "A", Color = "A" };

        var result = await VehicleHandlers.UpdateVehicle(1, mockHttp.Object, updateReq, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsConflict_WhenNewPlateExistsOnOtherCar()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "MY-CAR", Make="A", Model="A", Color="A", CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.Vehicles.Add(new VehicleModel { Id = 2, UserId = 50, LicensePlate = "TAKEN-PLATE", Make="B", Model="B", Color="B", CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(50);
        var updateReq = new VehicleModel { LicensePlate = "TAKEN-PLATE", Make = "A", Model = "A", Color = "A" };

        var result = await VehicleHandlers.UpdateVehicle(1, mockHttp.Object, updateReq, db);

        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsNotFound_WhenIdDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = CreateMockHttp(50);
        var updateReq = new VehicleModel { LicensePlate = "ABC", Make = "A", Model = "A", Color = "A" };

        var result = await VehicleHandlers.UpdateVehicle(999, mockHttp.Object, updateReq, db);

        Assert.IsType<NotFound<string>>(result);
    }

    // --- DELETE TESTS ---

    
    [Fact]
    public async Task DeleteVehicle_ReturnsOk_WhenDeletingOwnVehicle()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "DEL-ME", Make="A", Model="A", Color="A", CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(50);

        var result = await VehicleHandlers.DeleteVehicle(1, mockHttp.Object, db);

        Assert.False(result is NotFoundResult);
        Assert.False(result is UnauthorizedHttpResult);
        
       
        Assert.Empty(db.Vehicles);
    }

    [Fact]
    public async Task DeleteVehicle_ReturnsUnauthorized_WhenDeletingOthersVehicle()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "DONT-TOUCH", Make="A", Model="A", Color="A", CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(99); 

        var result = await VehicleHandlers.DeleteVehicle(1, mockHttp.Object, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Single(db.Vehicles);
    }
}