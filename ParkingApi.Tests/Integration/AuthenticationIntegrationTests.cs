using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using V2;
using System;
using System.Net;

namespace ParkingApi.Tests.Integration;

public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_NewUser_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            username = "newuser_$" + Guid.NewGuid().ToString("N")[..8],
            password = "Password123!",
            name = "New Test User",
            PhoneNumber = "0612345678",
            email = "newuser_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com",
            birthYear = 1990
        };

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        Assert.True(result.TryGetProperty("id", out var idProperty));
        Assert.True(idProperty.GetInt32() > 0);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var email = "duplicate_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        
        var firstRequest = new
        {
            username = "user1_$" + Guid.NewGuid().ToString("N")[..8],
            password = "Password123!",
            name = "First User",
            PhoneNumber = "0612345678",
            email = email,
            birthYear = 1990
        };

        var secondRequest = new
        {
            username = "user2_$" + Guid.NewGuid().ToString("N")[..8],
            password = "Password123!",
            name = "Second User",
            PhoneNumber = "0698765432",
            email = email, // Same email
            birthYear = 1995
        };

        // Act - First registration should succeed
        var firstResponse = await _client.PostAsJsonAsync("/register", firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act - Second registration should fail
        var secondResponse = await _client.PostAsJsonAsync("/register", secondRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        
        var content = await secondResponse.Content.ReadAsStringAsync();
        Assert.Contains("already exists", content);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        // Arrange
        var username = "duplicateuser_$" + Guid.NewGuid().ToString("N")[..8];
        
        var firstRequest = new
        {
            username = username,
            password = "Password123!",
            name = "First User",
            PhoneNumber = "0612345678",
            email = "first_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com",
            birthYear = 1990
        };

        var secondRequest = new
        {
            username = username, // Same username
            password = "Password123!",
            name = "Second User",
            PhoneNumber = "0698765432",
            email = "second_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com",
            birthYear = 1995
        };

        // Act
        var firstResponse = await _client.PostAsJsonAsync("/register", firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync("/register", secondRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            username = "testuser_$" + Guid.NewGuid().ToString("N")[..8],
            password = "Password123!",
            name = "Test User",
            PhoneNumber = "0612345678",
            email = "invalid-email", // Invalid email format
            birthYear = 1990
        };

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange - Create user first
        var username = "loginuser_$" + Guid.NewGuid().ToString("N")[..8];
        var email = "login_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        
        var registerRequest = new
        {
            username = username,
            password = "Password123!",
            name = "Login Test User",
            PhoneNumber = "0612345678",
            email = email,
            birthYear = 1990
        };

        var registerResponse = await _client.PostAsJsonAsync("/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginRequest = new { username = username, password = "Password123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        
        Assert.True(result.TryGetProperty("token", out var tokenProperty));
        Assert.False(string.IsNullOrEmpty(tokenProperty.GetString()));
        
        // Verify token is JWT format (contains dots)
        var token = tokenProperty.GetString()!;
        Assert.Contains(".", token);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new { username = "nonexistentuser", password = "wrongpassword" };

        // Act
        var response = await _client.PostAsJsonAsync("/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new { username = "nonexistent_$" + Guid.NewGuid().ToString("N")[..8], password = "Password123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var request = new { username = "", password = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
