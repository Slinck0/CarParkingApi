using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using V2.Data;
using V2.Models;
using V2.Helpers;
using V2.Services;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace ParkingApi.Tests.Handlers;

public class UserHandlerTests
{


    [Fact]
    public void Register_ReturnsCreated_WhenDataIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new RegisterUserRequest("NieuweUser", "Wachtwoord123", "Piet Piraat", "0612345678", "piet@schip.nl", 1980);

        var result = UserHandlers.Register(request, db);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(201, statusCodeResult.StatusCode);

        var savedUser = db.Users.FirstOrDefault(u => u.Email == "piet@schip.nl");
        Assert.NotNull(savedUser);
        Assert.Equal("NieuweUser", savedUser.Username);
    }

    [Fact]
    public void Register_ReturnsBadRequest_WhenRequiredFieldsAreMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new RegisterUserRequest("TestUser", "ValidPassword", null!, "123456789", "test@example.com", 2000); 

        var result = UserHandlers.Register(request, db);

        var badRequestResult = Assert.IsType<BadRequest<string>>(result);
        Assert.Contains("Name", badRequestResult.Value);
    }

    [Fact]
    public void Register_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var request = new RegisterUserRequest("Test", "Pass", "Naam", "06", "foute-email", 2000);

        var result = UserHandlers.Register(request, db);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public void Register_ReturnsConflict_WhenUserAlreadyExists()
    {
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

        var result = UserHandlers.Register(request, db);

        Assert.IsType<Conflict<string>>(result);
    }

    
    private TokenService CreateTokenService()
    {
        // We maken een echte settings collectie in het geheugen
        var mySettings = new Dictionary<string, string>
        {
            {"Jwt:Key", "super-secret-key-for-testing-only-12345"},
            {"Jwt:Issuer", "TestIssuer"},
            {"Jwt:Audience", "TestAudience"}
        };

        
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(mySettings!)
            .Build();

        return new TokenService(configuration);
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenCredentialsAreCorrect()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword("Geheim123!");
        
        db.Users.Add(new UserModel 
        { 
            Id = 1, 
            Username = "LoginUser", 
            Password = hashedPassword, 
            Email = "login@test.nl",
            Name = "Login Test",
            Phone = "06",
            Role = "User",
            Active = true
        });
        db.SaveChanges();

       
        var tokenService = CreateTokenService();
        var request = new LoginRequest("LoginUser", "Geheim123!"); 


        var result = await UserHandlers.Login(request, db, tokenService);


        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(200, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsIncorrect()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword("Geheim123!");
        db.Users.Add(new UserModel 
        { 
            Id = 1, Username = "LoginUser", Password = hashedPassword, 
            Email = "a@b.c", Name = "A", Phone = "06", Role = "User", Active = true
        });
        db.SaveChanges();

        var tokenService = CreateTokenService();
        var request = new LoginRequest("LoginUser", "FoutWachtwoord"); 


        var result = await UserHandlers.Login(request, db, tokenService);


        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserNotFound()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
    
        var tokenService = CreateTokenService();
        var request = new LoginRequest("Onbekend", "Wachtwoord");

        var result = await UserHandlers.Login(request, db, tokenService);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var tokenService = CreateTokenService();

        var request = new LoginRequest("", "");

        var result = await UserHandlers.Login(request, db, tokenService);

        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(400, statusCodeResult.StatusCode);
    }
}