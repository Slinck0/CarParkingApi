using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using V2.Models;
using Xunit;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Integration tests for Reservation endpoints using real Turso database.
/// </summary>
public class ReservationIntegrationTests : IntegrationTestBase
{
    public ReservationIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // Helpers are now in IntegrationTestBase

    #region POST /reservations - Create Reservation

    [Fact]
    public async Task CreateReservation_ReturnsCreated_WhenValidData()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vehicleId, licensePlate) = await CreateVehicleAsync(authClient);

        var request = new
        {
            LicensePlate = licensePlate,
            StartDate = DateTime.UtcNow.AddHours(1),
            EndDate = DateTime.UtcNow.AddHours(3),
            ParkingLot = parkingLotId
        };

        var response = await authClient.PostAsJsonAsync("/reservations", request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
        
        // Track the ID for cleanup
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.TryGetProperty("reservation", out var resProp) && resProp.TryGetProperty("id", out var idProp))
        {
            TrackReservation(idProp.GetString()!);
        }
    }

    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenInvalidDates()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vehicleId, licensePlate) = await CreateVehicleAsync(authClient);

        var request = new
        {
            LicensePlate = licensePlate,
            StartDate = DateTime.UtcNow.AddHours(3), // End before start
            EndDate = DateTime.UtcNow.AddHours(1),
            ParkingLot = parkingLotId
        };

        var response = await authClient.PostAsJsonAsync("/reservations", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenVehicleDoesNotExist()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();

        var request = new
        {
            LicensePlate = "XX-XX-XX", // Non-existent vehicle
            StartDate = DateTime.UtcNow.AddHours(1),
            EndDate = DateTime.UtcNow.AddHours(3),
            ParkingLot = parkingLotId
        };

        var response = await authClient.PostAsJsonAsync("/reservations", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_ReturnsConflict_WhenSpotIsTaken()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        
        // Vehicle 1
        var (v1, lp1) = await CreateVehicleAsync(authClient);
        
        // Reservation 1
        await CreateReservationAsync(authClient, parkingLotId, lp1);

        // Try to reserve overlapping time with the same vehicle
        var requestConflict = new
        {
            LicensePlate = lp1, // Same vehicle!
            StartDate = DateTime.UtcNow.AddHours(1), // Overlapping
            EndDate = DateTime.UtcNow.AddHours(3),
            ParkingLot = parkingLotId
        };

        var response = await authClient.PostAsJsonAsync("/reservations", requestConflict);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var request = new
        {
            LicensePlate = "AA-BB-12",
            StartDate = DateTime.UtcNow.AddHours(1),
            EndDate = DateTime.UtcNow.AddHours(2),
            ParkingLot = 1
        };

        var response = await _client.PostAsJsonAsync("/reservations", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /reservations/my-reservations - Get My Reservations

    [Fact]
    public async Task GetMyReservations_ReturnsList_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);

        await CreateReservationAsync(authClient, parkingLotId, lp, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3));
        
        // Create another vehicle for the same user to avoid conflict and create another reservation
        var (vid2, lp2) = await CreateVehicleAsync(authClient);
        await CreateReservationAsync(authClient, parkingLotId, lp2, DateTime.UtcNow.AddHours(4), DateTime.UtcNow.AddHours(6));

        var response = await authClient.GetAsync("/reservations/me");
        Assert.True(response.IsSuccessStatusCode);
        
        var reservations = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(reservations);
        Assert.True(reservations.Count >= 2); // Should have at least the two we created
    }

    [Fact]
    public async Task GetMyReservations_ReturnsEmptyList_WhenNoReservations()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/reservations/me");
        Assert.True(response.IsSuccessStatusCode);
        
        var reservations = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.Empty(reservations!);
    }
    
    [Fact]
    public async Task GetMyReservations_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/reservations/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /reservations/{id}

    [Fact]
    public async Task GetReservationById_ReturnsOk_WhenExists()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);

        var reservationId = await CreateReservationAsync(authClient, parkingLotId, lp);

        var response = await authClient.GetAsync($"/reservations/{reservationId}");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetReservationById_ReturnsNotFound_WhenDoesNotExist()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/reservations/non-existent-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region PUT /reservations/{id} - Update Reservation

    [Fact]
    public async Task UpdateReservation_ReturnsOk_WhenValidUpdate()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);
        
        var reservationId = await CreateReservationAsync(authClient, parkingLotId, lp);

        var updateRequest = new
        {
            LicensePlate = lp,
            ParkingLot = parkingLotId,
            StartDate = DateTime.UtcNow.AddHours(2),
            EndDate = DateTime.UtcNow.AddHours(5)
        };

        var response = await authClient.PutAsJsonAsync($"/reservations/{reservationId}", updateRequest);
        Assert.True(response.IsSuccessStatusCode);
    }
    
    [Fact]
    public async Task UpdateReservation_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        
        var updateRequest = new { EndDate = DateTime.UtcNow.AddHours(4) };
        
        var response = await authClient.PutAsJsonAsync("/reservations/non-existent-id", updateRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task UpdateReservation_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var updateRequest = new { EndDate = DateTime.UtcNow.AddHours(4) };
        var response = await _client.PutAsJsonAsync("/reservations/1", updateRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DELETE /reservations/{id} - Cancel Reservation

    [Fact]
    public async Task CancelReservation_ReturnsOk_WhenValidId()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);

        var reservationId = await CreateReservationAsync(authClient, parkingLotId, lp);

        var response = await authClient.DeleteAsync($"/reservations/{reservationId}");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task CancelReservation_ReturnsNotFound_WhenDoesNotExist()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.DeleteAsync("/reservations/non-existent-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelReservation_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.DeleteAsync("/reservations/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
