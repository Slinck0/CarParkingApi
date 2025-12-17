using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using ParkingApi.Tests.Helpers;
using ParkingImporter.Models;

namespace ParkingApi.Tests.Handlers;

public class ProfileHandlerTests
{
    private readonly Mock<HttpContext> _mockHttp;
    private readonly int _testUserId = 123;

    public ProfileHandlerTests()
    {
        _mockHttp = new Mock<HttpContext>();
        
        var claims = new Claim[] { new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString()) };
        var claimsIdentity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        _mockHttp.Setup(c => c.User).Returns(claimsPrincipal);
    }

    // --- SUCCESS TEST (HAPPY PATH) ---
    [Fact]
    public async Task UpdateProfile_ReturnsOk_WhenDataIsValid()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        // Bestaande gebruiker
        var existingUser = new User 
        { 
            Id = _testUserId, Username = "Oud", Name = "Oude Naam", Email = "oud@test.nl", Phone = "000", BirthYear = 1990,
            Password = "pw", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        };
        db.Users.Add(existingUser);
        db.SaveChanges();

        // Update verzoek
        var request = new UpdateProfileRequest("Nieuwe Naam", "nieuw@test.nl", "111", 1995);

        // Act
        var result = await ProfileHandlers.UpdateProfile(_mockHttp.Object, db, request);

        // Assert
        Assert.NotNull(result);
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        // Check of database is bijgewerkt
        var updatedUser = await db.Users.FindAsync(_testUserId);
        Assert.Equal("Nieuwe Naam", updatedUser.Name);
        Assert.Equal("nieuw@test.nl", updatedUser.Email);
        Assert.Equal(1995, updatedUser.BirthYear);
    }

    // --- SUCCESS TEST (GET) ---
    [Fact]
    public async Task GetProfile_ReturnsProfile_WhenUserExists()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        db.Users.Add(new User 
        { 
            Id = _testUserId, Username = "TestUser", Email = "a@b.com",
            Password = "Hash", Name = "Test", Phone = "0612345678", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        });
        db.SaveChanges();
        
        // Act
        var result = await ProfileHandlers.GetProfile(_mockHttp.Object, db);

        // Assert
        Assert.NotNull(result);
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        // Value uitlezen via reflectie (vanwege anoniem type)
        var valueProperty = result.GetType().GetProperty("Value");
        Assert.NotNull(valueProperty);
        
        var profile = valueProperty.GetValue(result);
        Assert.NotNull(profile);

        var profileType = profile.GetType();
        
        var idProp = profileType.GetProperty("Id");
        Assert.NotNull(idProp);
        Assert.Equal(_testUserId, (int)idProp.GetValue(profile, null)!);

        var usernameProp = profileType.GetProperty("Username");
        Assert.NotNull(usernameProp);
        Assert.Equal("TestUser", (string)usernameProp.GetValue(profile, null)!);
    }
    
    [Fact]
    public async Task UpdateProfile_ReturnsConflict_WhenNewEmailIsAlreadyInUse()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();

        var userToUpdate = new User 
        { 
            Id = _testUserId, Username = "Mijzelf", Email = "oud@voorbeeld.nl", BirthYear = 1990, 
            Name = "A", Phone = "123", Password = "pw", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true 
        };
        var otherUser = new User 
        { 
            Id = 456, Username = "Anderen", Email = "bezet@voorbeeld.nl",
            Name = "B", Phone = "456", Password = "pw", BirthYear = 2000, CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        };

        db.Users.AddRange(userToUpdate, otherUser);
        db.SaveChanges();

        var request = new UpdateProfileRequest("Mijn Nieuwe Naam", "bezet@voorbeeld.nl", "987654321", 1991);

        // Act
        var result = await ProfileHandlers.UpdateProfile(_mockHttp.Object, db, request);

        // Assert
        Assert.IsType<Conflict<string>>(result);
    }
}