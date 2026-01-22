using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using V2;

namespace ParkingApi.Tests.Integration;

public class HealthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HealthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/Health");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Parking API is running...", content);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/Health");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType.ToString());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task HealthEndpoint_DoesNotSupportOtherMethods(string method)
    {
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), "/Health");
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
