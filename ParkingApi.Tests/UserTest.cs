
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults; 
using ParkingImporter.Models;
using ParkingApi.Tests.Helpers;

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
    public void Register_ReturnsConflict_WhenUserAlreadyExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        db.Users.Add(new User 
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
}