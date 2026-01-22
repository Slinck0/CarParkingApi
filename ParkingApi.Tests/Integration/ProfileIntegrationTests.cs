using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using V2;
using System;
using System.Net;

namespace ParkingApi.Tests.Integration;

public class ProfileIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProfileIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<string> AuthenticateUser()
    {
        var username = "profileuser_$" + Guid.NewGuid().ToString("N")[..8];
        var email = "profile_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        
        var registerRequest = new
        {
            username = username,
            password = "Password123!",
            name = "Profile Test User",
            PhoneNumber = "0612345678",
            email = email,
            birthYear = 1990
        };

        await _client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new { username = username, password = "Password123!" };
        var loginResponse = await _client.PostAsJsonAsync("/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var tokenObject = JsonSerializer.Deserialize<JsonElement>(loginContent);
        return tokenObject.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task GetProfile_WithValidToken_ReturnsProfile()
    {
        // Arrange
        var token = await AuthenticateUser();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/profile");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(content));
    }

    [Fact]
    public async Task GetProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/profile");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithValidToken_UpdatesProfile()
    {
        // Arrange
        var token = await AuthenticateUser();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var updateRequest = new
        {
            name = "Updated Name",
            email = "updated_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com",
            PhoneNumber = "0698765432",
            birthYear = 1995
        };

        // Act
        var response = await _client.PutAsJsonAsync("/profile", updateRequest);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UpdateProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var updateRequest = new
        {
            name = "Updated Name",
            email = "updated@test.com",
            PhoneNumber = "0698765432",
            birthYear = 1995
        };

        // Act
        var response = await _client.PutAsJsonAsync("/profile", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
