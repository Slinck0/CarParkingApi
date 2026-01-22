using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using V2;
using System;
using System.Net;

namespace ParkingApi.Tests.Integration;

public class VehicleIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public VehicleIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<string> AuthenticateUser()
    {
        var username = "vehicleuser_$" + Guid.NewGuid().ToString("N")[..8];
        var email = "vehicle_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        
        var registerRequest = new
        {
            username = username,
            password = "Password123!",
            name = "Vehicle Test User",
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
    public async Task CreateVehicle_WithValidToken_CreatesVehicle()
    {
        // Arrange
        var token = await AuthenticateUser();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var vehicleRequest = new
        {
            licensePlate = "TEST-$" + Guid.NewGuid().ToString("N")[..8],
            make = "Toyota",
            model = "Corolla",
            color = "Blue"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/vehicles", vehicleRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(content));
    }

    [Fact]
    public async Task CreateVehicle_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var vehicleRequest = new
        {
            licensePlate = "TEST-123",
            make = "Toyota",
            model = "Corolla",
            color = "Blue"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/vehicles", vehicleRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_WithValidToken_ReturnsUserVehicles()
    {
        // Arrange
        var token = await AuthenticateUser();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/vehicles");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(content));
    }

    [Fact]
    public async Task GetVehicles_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/vehicles");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
