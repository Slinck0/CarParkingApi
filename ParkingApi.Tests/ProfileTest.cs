using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using V2.Data;
using V2.Models;
using V2.Handlers;
using V2.Services;
using V2.Helpers;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

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


    [Fact]
    public async Task UpdateProfile_ReturnsOk_WhenDataIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var existingUser = new UserModel
        { 
            Id = _testUserId, Username = "Oud", Name = "Oude Naam", Email = "oud@test.nl", Phone = "000", BirthYear = 1990,
            Password = "pw", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        };
        db.Users.Add(existingUser);
        db.SaveChanges();

        var request = new UpdateProfileRequest("Nieuwe Naam", "nieuw@test.nl", "111", 1995);

        var result = await ProfileHandlers.UpdateProfile(_mockHttp.Object, db, request);

        Assert.NotNull(result);
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var updatedUser = await db.Users.FindAsync(_testUserId);
        Assert.Equal("Nieuwe Naam", updatedUser.Name);
        Assert.Equal("nieuw@test.nl", updatedUser.Email);
        Assert.Equal(1995, updatedUser.BirthYear);
    }

    [Fact]
    public async Task GetProfile_ReturnsProfile_WhenUserExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel
        {
            Id = _testUserId, Username = "TestUser", Email = "a@b.com",
            Password = "Hash", Name = "Test", Phone = "0612345678", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        });
        db.SaveChanges();
        
        var result = await ProfileHandlers.GetProfile(_mockHttp.Object, db);

        Assert.NotNull(result);
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

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
        using var db = DbContextHelper.GetInMemoryDbContext();

        var userToUpdate = new UserModel
        { 
            Id = _testUserId, Username = "Mijzelf", Email = "oud@voorbeeld.nl", BirthYear = 1990, 
            Name = "A", Phone = "123", Password = "pw", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true 
        };
        var otherUser = new UserModel
        { 
            Id = 456, Username = "Anderen", Email = "bezet@voorbeeld.nl",
            Name = "B", Phone = "456", Password = "pw", BirthYear = 2000, CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        };

        db.Users.AddRange(userToUpdate, otherUser);
        db.SaveChanges();

        var request = new UpdateProfileRequest("Mijn Nieuwe Naam", "bezet@voorbeeld.nl", "987654321", 1991);

        var result = await ProfileHandlers.UpdateProfile(_mockHttp.Object, db, request);

        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public async Task DeleteProfile_ReturnsOk_WhenUserExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel
        {
            Id = _testUserId, Username = "ToDelete", Email = "delete@test.nl",
            Password = "Hash", Name = "DeleteMe", Phone = "0612345678", 
            CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        });
        db.SaveChanges();

        var result = await ProfileHandlers.DeleteProfile(_mockHttp.Object, db);

        Assert.NotNull(result);
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var deletedUser = await db.Users.FindAsync(_testUserId);
        Assert.Null(deletedUser);
    }

    [Fact]
    public async Task DeleteProfile_ReturnsNotFound_WhenUserDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        // No user added, so the user does not exist

        var result = await ProfileHandlers.DeleteProfile(_mockHttp.Object, db);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task GetProfile_ReturnsNotFound_WhenUserDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        // No user added

        var result = await ProfileHandlers.GetProfile(_mockHttp.Object, db);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task GetProfile_ReturnsUnauthorized_WhenNoUserClaim()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var mockHttp = new Moq.Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // No claims

        var result = await ProfileHandlers.GetProfile(mockHttp.Object, db);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel
        {
            Id = _testUserId, Username = "TestUser", Email = "test@test.nl",
            Password = "pw", Name = "Test", Phone = "123", BirthYear = 1990, 
            CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        });
        db.SaveChanges();

        // Test with missing Name
        var request = new UpdateProfileRequest("", "test@test.nl", "123", 1990);
        var result = await ProfileHandlers.UpdateProfile(_mockHttp.Object, db, request);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsBadRequest_WhenEmailInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel
        {
            Id = _testUserId, Username = "TestUser", Email = "test@test.nl",
            Password = "pw", Name = "Test", Phone = "123", BirthYear = 1990, 
            CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        });
        db.SaveChanges();

        // Test with invalid email format
        var request = new UpdateProfileRequest("ValidName", "invalidemail", "123", 1990);
        var result = await ProfileHandlers.UpdateProfile(_mockHttp.Object, db, request);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsNotFound_WhenUserDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        // No user added

        var request = new UpdateProfileRequest("ValidName", "valid@email.com", "123", 1990);
        var result = await ProfileHandlers.UpdateProfile(_mockHttp.Object, db, request);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task UpdateState_TogglesActiveState()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var targetUserId = 999;

        db.Users.Add(new UserModel
        {
            Id = targetUserId, Username = "TargetUser", Email = "target@test.nl",
            Password = "pw", Name = "Target", Phone = "123",
            CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        });
        db.SaveChanges();

        var result = await ProfileHandlers.UpdateState(_mockHttp.Object, db, targetUserId);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        var updatedUser = await db.Users.FindAsync(targetUserId);
        Assert.False(updatedUser!.Active); // Was true, now should be false
    }

    [Fact]
    public async Task UpdateState_ReturnsNotFound_WhenUserDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var result = await ProfileHandlers.UpdateState(_mockHttp.Object, db, 999);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task UpdateState_ReturnsUnauthorized_WhenNoUserClaim()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var mockHttp = new Moq.Mock<HttpContext>();
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity())); // No claims

        var result = await ProfileHandlers.UpdateState(mockHttp.Object, db, 1);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}