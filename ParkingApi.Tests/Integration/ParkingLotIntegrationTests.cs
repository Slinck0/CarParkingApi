using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using V2.Models;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Integration tests for Parking Lot endpoints using real Turso database.
/// </summary>
public class ParkingLotIntegrationTests : IntegrationTestBase
{
    public ParkingLotIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region Helper Methods

    // Helpers are now in IntegrationTestBase

    #endregion

    #region GET /parking-lots/{id} - Get Parking Lot By ID (Public)

    [Fact]
    public async Task GetParkingLotById_ReturnsOk_WhenIdExists()
    {
        // Create a parking lot first
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        var createRequest = new
        {
            Name = $"TestLot_{Guid.NewGuid():N}"[..15],
            Location = "Rotterdam",
            Address = "Test Street 1",
            Capacity = 100,
            Reserved = 0,
            Tariff = 5.0m,
            DayTariff = 25.0m,
            Lat = 51.9225,
            Lng = 4.47917,
            Status = "Open"
        };
        var createResponse = await adminClient.PostAsJsonAsync("/parking-lots", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createContent);
        var lotId = doc.RootElement.GetProperty("id").GetInt32();

        // Get the parking lot (public endpoint - no auth needed)
        var response = await _client.GetAsync($"/parking-lots/{lotId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetParkingLotById_ReturnsNotFound_WhenIdDoesNotExist()
    {
        var response = await _client.GetAsync("/parking-lots/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST /parking-lots - Create Parking Lot (ADMIN)

    [Fact]
    public async Task CreateParkingLot_ReturnsCreated_WhenValidDataAndAdmin()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var request = new
        {
            Name = $"NewLot_{Guid.NewGuid():N}"[..15],
            Location = "Amsterdam",
            Address = "Dam Square 1",
            Capacity = 200,
            Reserved = 0,
            Tariff = 6.0m,
            DayTariff = 30.0m,
            Lat = 52.3676,
            Lng = 4.9041,
            Status = "Open"
        };

        var response = await authClient.PostAsJsonAsync("/parking-lots", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateParkingLot_ReturnsBadRequest_WhenMissingRequiredFields()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var request = new
        {
            Name = "",
            Location = "Test"
        };

        var response = await authClient.PostAsJsonAsync("/parking-lots", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateParkingLot_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var request = new
        {
            Name = "Test Lot",
            Location = "Test",
            Address = "Test 1",
            Capacity = 100
        };

        var response = await _client.PostAsJsonAsync("/parking-lots", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateParkingLot_ReturnsForbidden_WhenNotAdmin()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var request = new
        {
            Name = "Test Lot",
            Location = "Test",
            Address = "Test 1",
            Capacity = 100,
            Tariff = 5.0m
        };

        var response = await authClient.PostAsJsonAsync("/parking-lots", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region PUT /parking-lots/{id}/status - Update Parking Lot Status (ADMIN)

    [Fact]
    public async Task UpdateParkingLotStatus_ReturnsOk_WhenValidIdAndAdmin()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        // Create a parking lot first
        var createRequest = new
        {
            Name = $"UpdateLot_{Guid.NewGuid():N}"[..15],
            Location = "Utrecht",
            Address = "Central Station 1",
            Capacity = 150,
            Reserved = 0,
            Tariff = 5.5m,
            DayTariff = 27.5m,
            Lat = 52.0907,
            Lng = 5.1214,
            Status = "Open"
        };
        var createResponse = await authClient.PostAsJsonAsync("/parking-lots", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createContent);
        var lotId = doc.RootElement.GetProperty("id").GetInt32();

        // Update status
        var updateRequest = new { Status = "Closed" };
        var response = await authClient.PutAsJsonAsync($"/parking-lots/{lotId}/status", updateRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateParkingLotStatus_ReturnsNotFound_WhenInvalidId()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var updateRequest = new { Status = "Closed" };
        var response = await authClient.PutAsJsonAsync("/parking-lots/999999/status", updateRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateParkingLotStatus_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var updateRequest = new { Status = "Closed" };
        var response = await _client.PutAsJsonAsync("/parking-lots/1/status", updateRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DELETE /parking-lots/{id} - Delete Parking Lot (ADMIN)

    [Fact]
    public async Task DeleteParkingLot_ReturnsOk_WhenValidIdAndAdmin()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        // Create a parking lot first
        var createRequest = new
        {
            Name = $"DeleteLot_{Guid.NewGuid():N}"[..15],
            Location = "Den Haag",
            Address = "Binnenhof 1",
            Capacity = 80,
            Reserved = 0,
            Tariff = 7.0m,
            DayTariff = 35.0m,
            Lat = 52.0799,
            Lng = 4.3113,
            Status = "Open"
        };
        var createResponse = await authClient.PostAsJsonAsync("/parking-lots", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createContent);
        var lotId = doc.RootElement.GetProperty("id").GetInt32();

        // Delete the parking lot
        var response = await authClient.DeleteAsync($"/parking-lots/{lotId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteParkingLot_ReturnsNotFound_WhenInvalidId()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.DeleteAsync("/parking-lots/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteParkingLot_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.DeleteAsync("/parking-lots/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
