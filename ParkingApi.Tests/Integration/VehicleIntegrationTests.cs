using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Integration tests for Vehicle endpoints using real Turso database.
/// </summary>
public class VehicleIntegrationTests : IntegrationTestBase
{
    public VehicleIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region Helper Methods

    // RegisterAndGetTokenAsync and CreateAuthenticatedClient are now in IntegrationTestBase

    #endregion

    #region PUT /vehicles/{id} - Update Vehicle

    [Fact]
    public async Task UpdateVehicle_ReturnsOk_WhenValidUpdate()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var (vehicleId, _) = await CreateVehicleAsync(authClient);

        var g = Guid.NewGuid().ToString("N").ToUpper();
        var plate = $"{g.Substring(0, 2)}-{g.Substring(2, 2)}-{g.Substring(4, 2)}";

        var updateRequest = new
        {
            LicensePlate = plate, // Unique plate to avoid Conflict
            Make = "BMW",
            Model = "X5",
            Color = "Blue",
            Year = 2024
        };

        var response = await authClient.PutAsJsonAsync($"/vehicles/{vehicleId}", updateRequest);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsNotFound_WhenInvalidId()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var updateRequest = new
        {
            Make = "BMW",
            Model = "X5",
            Color = "Blue",
            Year = 2024
        };

        var response = await authClient.PutAsJsonAsync("/vehicles/999999", updateRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsUnauthorized_WhenNotOwner()
    {
        // User 1 creates vehicle
        var token1 = await RegisterAndGetTokenAsync();
        var client1 = CreateAuthenticatedClient(token1);
        var (vehicleId, _) = await CreateVehicleAsync(client1);

        // User 2 tries to update
        var token2 = await RegisterAndGetTokenAsync();
        var client2 = CreateAuthenticatedClient(token2);

        var updateRequest = new
        {
            Make = "Audi",
            Model = "A4",
            Color = "Red",
            Year = 2022
        };

        var response = await client2.PutAsJsonAsync($"/vehicles/{vehicleId}", updateRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); // Or Forbidden, depending on implementation 
        // Note: Implementation usually returns Unauthorized (401) or Forbidden (403). 
        // Here we accept Unauthorized based on previous tests.
    }

    [Fact]
    public async Task UpdateVehicle_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var updateRequest = new { Make = "BMW" };
        var response = await _client.PutAsJsonAsync("/vehicles/1", updateRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DELETE /vehicles/{id} - Delete Vehicle

    [Fact]
    public async Task DeleteVehicle_ReturnsOk_WhenValidId()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var (vehicleId, _) = await CreateVehicleAsync(authClient);

        var response = await authClient.DeleteAsync($"/vehicles/{vehicleId}");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DeleteVehicle_ReturnsNotFound_WhenInvalidId()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.DeleteAsync("/vehicles/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVehicle_ReturnsUnauthorized_WhenNotOwner()
    {
        var token1 = await RegisterAndGetTokenAsync();
        var client1 = CreateAuthenticatedClient(token1);
        var (vehicleId, _) = await CreateVehicleAsync(client1);

        var token2 = await RegisterAndGetTokenAsync();
        var client2 = CreateAuthenticatedClient(token2);

        var response = await client2.DeleteAsync($"/vehicles/{vehicleId}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVehicle_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.DeleteAsync("/vehicles/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /vehicles/my-vehicles - Get My Vehicles

    [Fact]
    public async Task GetMyVehicles_ReturnsList_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        await CreateVehicleAsync(authClient);
        await CreateVehicleAsync(authClient);

        var response = await authClient.GetAsync("/vehicles");
        Assert.True(response.IsSuccessStatusCode);
        
        var vehicles = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(vehicles);
        Assert.True(vehicles.Count >= 2);
    }

    [Fact]
    public async Task GetMyVehicles_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
