using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ParkingImporter.Models;
using ParkingApi.Tests.Helpers;
using System.Security.Claims;

namespace ParkingApi.Tests.Handlers;

public class VehicleHandlerTests
{
    [Fact]
    public async Task CreateVehicle_ReturnsCreated_WhenDataIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var mockHttp = new Mock<HttpContext>();
        var claims = new Claim[] { new Claim(ClaimTypes.NameIdentifier, "99") }; // UserId 99
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims)));

        var vehicle = new Vehicle 
        { 
            LicensePlate = "XYZ-999", Make = "Tesla", Model = "Model 3", Color = "Zwart" 
        };

        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, vehicle, db);

        var createdResult = Assert.IsType<Created<Vehicle>>(result);
        Assert.Equal(201, createdResult.StatusCode);
        
        var savedVehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == "XYZ-999");
        Assert.NotNull(savedVehicle);
        Assert.Equal(99, savedVehicle.UserId); 
    }

    [Fact]
    public async Task CreateVehicle_ReturnsConflict_WhenLicensePlateExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Vehicles.Add(new Vehicle 
        { 
            Id = 1, LicensePlate = "ABC-123", Make = "Ford", Model = "Fiesta", Color = "Blauw", UserId = 1, CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });
        db.SaveChanges();

        var mockHttp = new Mock<HttpContext>();
        var claims = new Claim[] { new Claim(ClaimTypes.NameIdentifier, "123") };
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims)));
        
        var request = new Vehicle 
        { 
            LicensePlate = "ABC-123", Make = "Ford", Model = "Focus", Color = "Rood" 
        };

        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, request, db);

        Assert.IsType<Conflict<string>>(result);
    }
}