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
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = CreateMockHttp(99); 

        var vehicle = new VehicleModel 
        { 
            LicensePlate = "XZ-99-88", 
            Make = "Tesla", 
            Model = "Model 3", 
            Color = "Zwart",
            Year = 2022 
        };

        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, vehicle, db);

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

        var vehicle = new VehicleModel
        { 
            LicensePlate = "AA-99-BB", 
            Make = "B", 
            Model = "C", 
            Color = "D",
            Year = 2022
        };

        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, vehicle, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task CreateVehicle_ReturnsConflict_WhenLicensePlateExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel 
        { 
            Id = 1, 
            LicensePlate = "AB-12-CD", 
            Make = "F", 
            Model = "F", 
            Color = "B", 
            UserId = 1, 
            Year = 2020,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now) 
        });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(123);
        var request = new VehicleModel 
        { 
            LicensePlate = "AB-12-CD", 
            Make = "F", 
            Model = "Focus", 
            Color = "Rood",
            Year = 2022
        };

        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, request, db);

        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public async Task CreateVehicle_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = CreateMockHttp(99);

        // Test 1: Geen kenteken
        var v1 = new VehicleModel { LicensePlate = "", Make = "A", Model = "B", Color = "C", Year = 2022 };
        var r1 = await VehicleHandlers.CreateVehicle(mockHttp.Object, v1, db);
        Assert.IsType<BadRequest<string>>(r1);

        // Test 2: Geen Merk
        var v2 = new VehicleModel { LicensePlate = "AA-99-BB", Make = "", Model = "B", Color = "C", Year = 2022 };
        var r2 = await VehicleHandlers.CreateVehicle(mockHttp.Object, v2, db);
        Assert.IsType<BadRequest<string>>(r2);
    }



    [Fact]
    public async Task GetMyVehicles_ReturnsOnlyMyVehicles()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 10, LicensePlate = "AA-11-BB", Make="A", Model="A", Color="A", Year = 2022, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.Vehicles.Add(new VehicleModel { Id = 2, UserId = 20, LicensePlate = "CC-22-DD", Make="B", Model="B", Color="B", Year = 2022, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(10); 

        var result = await VehicleHandlers.GetMyVehicles(mockHttp.Object, db);

        var okResult = Assert.IsType<Ok<List<VehicleModel>>>(result);
        var vehicles = Assert.IsType<List<VehicleModel>>(okResult.Value);
        Assert.Single(vehicles); 
        Assert.Equal("AA-11-BB", vehicles[0].LicensePlate);
    }


    [Fact]
    public async Task UpdateVehicle_ReturnsOk_WhenUpdatingOwnVehicle()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "XX-11-YY", Make = "Old", Model = "Old", Color = "Old", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(50); 
        var updateReq = new VehicleModel { LicensePlate = "XX-22-ZZ", Make = "New", Model = "New", Color = "New", Year = 2022 };

        var result = await VehicleHandlers.UpdateVehicle(1, mockHttp.Object, updateReq, db);

        Assert.IsType<Ok<VehicleModel>>(result);
        var updated = await db.Vehicles.FindAsync(1);
        Assert.Equal("XX-22-ZZ", updated!.LicensePlate);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsUnauthorized_WhenUpdatingOthersVehicle()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "AA-00-BB", Make="A", Model="A", Color="A", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(99); // Hacker
        var updateReq = new VehicleModel { LicensePlate = "CC-00-DD", Make = "A", Model = "A", Color = "A", Year = 2022 };

        var result = await VehicleHandlers.UpdateVehicle(1, mockHttp.Object, updateReq, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsConflict_WhenNewPlateExistsOnOtherCar()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "AA-11-BB", Make="A", Model="A", Color="A", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.Vehicles.Add(new VehicleModel { Id = 2, UserId = 50, LicensePlate = "CC-22-DD", Make="B", Model="B", Color="B", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(50);
        var updateReq = new VehicleModel { LicensePlate = "CC-22-DD", Make = "A", Model = "A", Color = "A", Year = 2022 };

        var result = await VehicleHandlers.UpdateVehicle(1, mockHttp.Object, updateReq, db);

        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsNotFound_WhenIdDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = CreateMockHttp(50);
        var updateReq = new VehicleModel { LicensePlate = "AA-11-BB", Make = "A", Model = "A", Color = "A", Year = 2022 };

        var result = await VehicleHandlers.UpdateVehicle(999, mockHttp.Object, updateReq, db);

        Assert.IsType<NotFound<string>>(result);
    }

    // --- DELETE TESTS ---

    
    [Fact]
    public async Task DeleteVehicle_ReturnsOk_WhenDeletingOwnVehicle()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "AA-99-ZZ", Make="A", Model="A", Color="A", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
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
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "BB-88-YY", Make="A", Model="A", Color="A", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(99); 

        var result = await VehicleHandlers.DeleteVehicle(1, mockHttp.Object, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Single(db.Vehicles);
    }

    [Fact]
    public async Task GetMyVehicles_ReturnsEmptyList_WhenUserHasNoVehicles()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = CreateMockHttp(10);

        var result = await VehicleHandlers.GetMyVehicles(mockHttp.Object, db);

        var okResult = Assert.IsType<Ok<List<VehicleModel>>>(result);
        Assert.Empty(okResult.Value!);
    }

    [Fact]
    public async Task GetMyVehicles_ReturnsUnauthorized_WhenNoUserClaim()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = new Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); 

        var result = await VehicleHandlers.GetMyVehicles(mockHttp.Object, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task DeleteVehicle_ReturnsNotFound_WhenVehicleDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var mockHttp = CreateMockHttp(50);

        var result = await VehicleHandlers.DeleteVehicle(999, mockHttp.Object, db);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Vehicles.Add(new VehicleModel { Id = 1, UserId = 50, LicensePlate = "XX-11-YY", Make = "Old", Model = "Old", Color = "Old", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        var mockHttp = CreateMockHttp(50);
        
        // Missing Color
        var updateReq = new VehicleModel { LicensePlate = "XX-22-ZZ", Make = "New", Model = "New", Color = "", Year = 2022 };
        var result = await VehicleHandlers.UpdateVehicle(1, mockHttp.Object, updateReq, db);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task AdminGetOrganizationVehicles_ReturnsNotFound_WhenOrganizationDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var result = await VehicleHandlers.AdminGetOrganizationVehicles(999, db);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task AdminGetOrganizationVehicles_ReturnsEmptyList_WhenNoVehiclesInOrg()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Test Org" });
        db.SaveChanges();

        var result = await VehicleHandlers.AdminGetOrganizationVehicles(1, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var valueProperty = result.GetType().GetProperty("Value");
        var value = valueProperty?.GetValue(result);
        var countProp = value?.GetType().GetProperty("count");
        Assert.Equal(0, countProp?.GetValue(value));
    }
}