using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults; // Nodig voor de nieuwe return types (BadRequest<T>, Conflict<T>)
using V2.Data;
using V2.Models;
using V2.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using ParkingApi.Tests.Helpers;

public static class DbContextHelper
{
    public static AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}

public class UserHandlerTests
{
    [Fact]
    public void Register_ReturnsCreated_WhenDataIsValid()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new RegisterUserRequest("NieuweUser", "Wachtwoord123", "Piet Piraat", "0612345678", "piet@schip.nl", 1980);

        // Act
        var result = UserHandlers.Register(request, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, statusCodeResult.StatusCode);

        var savedUser = db.Users.FirstOrDefault(u => u.Email == "piet@schip.nl");
        Assert.NotNull(savedUser);
        Assert.Equal("NieuweUser", savedUser.Username);
    }

    [Fact]
    public void Register_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new RegisterUserRequest(
            "TestUser",
            "ValidPassword",
            "Test Name",
            "123456789",
            "invalid-email-format",
            2000
        );

        // Act
        var result = UserHandlers.Register(request, db);

        // Assert
        var badRequest = Assert.IsType<BadRequest<string>>(result);
        Assert.Contains("Invalid email format", badRequest.Value);
    }

    [Fact]
    public void Register_ReturnsConflict_WhenEmailAlreadyExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Users.Add(new UserModel
        {
            Id = 1,
            Username = "ExistingUser",
            Email = "existing@mail.com",
            Password = "Hash",
            Name = "Test",
            Phone = "0612345678",
            BirthYear = 1990,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            Active = true
        });

        db.SaveChanges();

        var request = new RegisterUserRequest("NewUser", "ValidPassword", "New Name", "123456789", "existing@mail.com", 1995);

        var result = UserHandlers.Register(request, db);

        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public void Register_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var request = new RegisterUserRequest("TestUser", "ValidPassword", null!, "123456789", "test@example.com", 2000); 

        // Act
        var result = UserHandlers.Register(request, db);

        var badRequestResult = Assert.IsType<BadRequest<string>>(result);
        Assert.Contains("Name", badRequestResult.Value);
    }

    [Fact]
    public void Register_ReturnsConflict_WhenUserAlreadyExists()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        db.Users.Add(new UserModel 
        { 
            Id = 1, Username = "BestaandeGebruiker", Email = "test@example.com",
            Password = "Hash", Name = "Test", Phone = "0612345678", CreatedAt = DateOnly.FromDateTime(DateTime.Now), Active = true
        });
        db.SaveChanges();

        var request = new RegisterUserRequest(
            "BestaandeGebruiker", "ValidPassword", "Nieuwe Naam", "123456789", "nieuw@voorbeeld.nl", 2000
        );

        // Act
        var result = UserHandlers.Register(request, db);

        // Assert
        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_whenFieldsAreMissing()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var request = new LoginRequest("", ""); // Lege velden
        var tokenService = new TokenService(TestConfigHelper.GetTestConfiguration());

        // Act
        var result = await UserHandlers.Login(request, db, tokenService);

        // Assert
        var badRequest = Assert.IsAssignableFrom<IResult>(result);
        var valueProperty = badRequest.GetType().GetProperty("Value");
        Assert.NotNull(valueProperty);

        var value = valueProperty.GetValue(badRequest);
        var messageProperty = value!.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var message = messageProperty.GetValue(value) as string;
        Assert.Contains("Username and Password are required", message!);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserDoesNotExist()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        var tokenService = new TokenService(TestConfigHelper.GetTestConfiguration());
        
        var request = new LoginRequest("NonExistentUser", "SomePassword");

        // Act
        var result = await UserHandlers.Login(request, db, tokenService);

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsIncorrect()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        var tokenService = new TokenService(TestConfigHelper.GetTestConfiguration());
        
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword");
        db.Users.Add(new UserModel
        {
            Username = "TestUser",
            Password = BCrypt.Net.BCrypt.HashPassword("Password123"),
            Name = "Test User",
            Phone = "0612345678",
            Email = "test@test.nl",
            BirthYear = 1990,
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        db.SaveChanges();

        var request = new LoginRequest("TestUser", "IncorrectPassword");

        // Act
        var result = await UserHandlers.Login(request, db, tokenService);

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsToken_WhenCredentialsAreValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var tokenService = new TokenService(TestConfigHelper.GetTestConfiguration());

        const string plainPassword = "Password123";

        db.Users.Add(new UserModel
        {
            Username = "TestUser",
            Password = BCrypt.Net.BCrypt.HashPassword(plainPassword),
            Name = "Test User",
            Phone = "0612345678",
            Email = "test@test.nl",
            BirthYear = 1990,
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        db.SaveChanges();

        var request = new LoginRequest("TestUser", plainPassword);

        // Act
        var result = await UserHandlers.Login(request, db, tokenService);

        // Assert
        var ok = Assert.IsAssignableFrom<IResult>(result);
    }
}

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
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        var existingUser = new UserModel 
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
        // 1. Check resultaat
        Assert.NotNull(result);
        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, statusCodeResult.StatusCode);

        // 2. Check of database is bijgewerkt
        var updatedUser = await db.Users.FindAsync(_testUserId);
        Assert.Equal("Nieuwe Naam", updatedUser.Name);
        Assert.Equal("nieuw@test.nl", updatedUser.Email);
        Assert.Equal(1995, updatedUser.BirthYear);
    }

    [Fact]
    public async Task GetProfile_ReturnsProfile_WhenUserExists()
    {
        // Arrange
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
        // Arrange
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

        // Act
        var result = await ProfileHandlers.UpdateProfile(_mockHttp.Object, db, request);

        // Assert
        Assert.IsType<Conflict<string>>(result);
    }
}

public class VehicleHandlerTests
{
    [Fact]
    public async Task CreateVehicle_ReturnsCreated_WhenDataIsValid()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Users.Add(new UserModel
        {
            Id = 99,
            Username = "TestUser",
            Email = "test@test.nl",
            Password = "pw",
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now),
            BirthYear = 1990,
            Name = "Test",
            Phone = "123"
        });
        db.SaveChanges();
        
        var mockHttp = new Mock<HttpContext>();
        var claims = new Claim[] { new Claim(ClaimTypes.NameIdentifier, "99") }; // UserId 99
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        var vehicle = new VehicleModel 
        { 
            LicensePlate = "XYZ-999", Make = "Tesla", Model = "Model 3", Color = "Zwart" 
        };

        // Act
        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, vehicle, db);

        // Assert
        // 1. Check Resultaat
        var createdResult = Assert.IsType<Created<VehicleModel>>(result);
        Assert.Equal(201, createdResult.StatusCode);
        
        // 2. Check Database
        var savedVehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == "XYZ-999");
        Assert.NotNull(savedVehicle);
        Assert.Equal(99, savedVehicle.UserId); // Check of de UserId correct uit de claims is gehaald
    }

    // --- ERROR TEST ---
    [Fact]
    public async Task CreateVehicle_ReturnsConflict_WhenLicensePlateExists()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Users.Add(new UserModel
        {
            Id = 123,
            Username = "OtherUser",
            Email = "other@test.nl",
            Password = "pw",
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now),
            BirthYear = 1990,
            Name = "Other",
            Phone = "456"
        });
        db.SaveChanges();

        db.Vehicles.Add(new VehicleModel
        { 
            Id = 1, LicensePlate = "ABC-123", Make = "Ford", Model = "Fiesta", Color = "Blauw", UserId = 1, CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });
        db.SaveChanges();

        var mockHttp = new Mock<HttpContext>();
        var claims = new Claim[] { new Claim(ClaimTypes.NameIdentifier, "123") };
        mockHttp.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));
        
        var request = new VehicleModel
        { 
            LicensePlate = "ABC-123", Make = "Ford", Model = "Focus", Color = "Rood" 
        };

        // Act
        var result = await VehicleHandlers.CreateVehicle(mockHttp.Object, request, db);

        // Assert
        Assert.IsType<Conflict<string>>(result);
    }
}

