using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;
using V2;

namespace ParkingApi.Tests.Integration;

public class AuthorizationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthorizationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData("/profile")]
    [InlineData("/vehicles")]
    [InlineData("/payments")]
    [InlineData("/reservations/me")]
    public async Task ProtectedEndpoints_WithoutToken_ReturnUnauthorized(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/profile", "PUT")]
    [InlineData("/profile", "DELETE")]
    [InlineData("/vehicles", "POST")]
    [InlineData("/payments", "POST")]
    public async Task ProtectedEndpoints_WithoutToken_ReturnUnauthorized_ForAllMethods(string endpoint, string method)
    {
        // Act
        HttpResponseMessage response;
        switch (method.ToUpper())
        {
            case "POST":
                response = await _client.PostAsJsonAsync(endpoint, new { });
                break;
            case "PUT":
                response = await _client.PutAsJsonAsync(endpoint, new { });
                break;
            case "DELETE":
                response = await _client.DeleteAsync(endpoint);
                break;
            default:
                response = await _client.GetAsync(endpoint);
                break;
        }

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_DoesNotRequireAuthentication_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/Health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Parking API is running...", content);
    }

    [Fact]
    public async Task RegisterEndpoint_DoesNotRequireAuthentication_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            username = "noauth_$" + Guid.NewGuid().ToString("N")[..8],
            password = "Password123!",
            name = "No Auth User",
            PhoneNumber = "0612345678",
            email = "noauth_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com",
            birthYear = 1990
        };

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task LoginEndpoint_DoesNotRequireAuthentication_ReturnsToken()
    {
        // Arrange - Create user first
        var username = "loginnoauth_$" + Guid.NewGuid().ToString("N")[..8];
        var email = "loginnoauth_$" + Guid.NewGuid().ToString("N")[..8] + "@test.com";
        
        var registerRequest = new
        {
            username = username,
            password = "Password123!",
            name = "Login No Auth User",
            PhoneNumber = "0612345678",
            email = email,
            birthYear = 1990
        };

        await _client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new { username = username, password = "Password123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ProtectedEndpoints_WithInvalidToken_ReturnUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.format");

        // Act & Assert
        var profileResponse = await _client.GetAsync("/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, profileResponse.StatusCode);

        var vehiclesResponse = await _client.GetAsync("/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, vehiclesResponse.StatusCode);

        var paymentsResponse = await _client.GetAsync("/payments");
        Assert.Equal(HttpStatusCode.Unauthorized, paymentsResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_WithExpiredToken_ReturnUnauthorized()
    {
        // Arrange - Create a token that looks expired (this is a simplified test)
        var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        // Act & Assert
        var profileResponse = await _client.GetAsync("/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, profileResponse.StatusCode);

        var vehiclesResponse = await _client.GetAsync("/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, vehiclesResponse.StatusCode);
    }
}

