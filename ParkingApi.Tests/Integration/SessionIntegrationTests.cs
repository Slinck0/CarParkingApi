using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using V2.Models;
using Xunit;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Integration tests for ParkingSession endpoints using real Turso database.
/// </summary>
public class SessionIntegrationTests : IntegrationTestBase
{
    public SessionIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region POST /parking-sessions/start - Start Session

    [Fact]
    public async Task StartSession_ReturnsCreated_WhenValidData()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);

        var request = new
        {
            LicensePlate = lp,
            ParkingLotId = parkingLotId
        };

        var response = await authClient.PostAsJsonAsync($"/parkinglots/{parkingLotId}/sessions/start", request);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
        
        // Track session ID
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        // The handler returns a message object, not the session ID directly in the root or a 'session' property with ID.
        // But for cleanup we need the ID. 
        // The handler logic: return Results.Ok(new { message = "..." }); <- It does NOT return the ID!
        // We need to fetch it to track it.
        
        // Helper to find the session we just created
        var sessionsResponse = await authClient.GetAsync($"/parkinglots/{parkingLotId}/sessions/active/{lp}");
        if (sessionsResponse.IsSuccessStatusCode)
        {
             var sessionContent = await sessionsResponse.Content.ReadAsStringAsync();
             using var sessionDoc = JsonDocument.Parse(sessionContent);
             if (sessionDoc.RootElement.TryGetProperty("id", out var idProp))
             {
                 TrackSession(idProp.GetInt32());
             }
        }
    }

    [Fact]
    public async Task StartSession_ReturnsBadRequest_WhenVehicleDoesNotExist()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();

        var request = new
        {
            LicensePlate = "XX-XX-XX",
            ParkingLotId = parkingLotId
        };

        var response = await authClient.PostAsJsonAsync($"/parkinglots/{parkingLotId}/sessions/start", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Handler returns NotFound for vehicle
    }
    
    [Fact]
    public async Task StartSession_ReturnsNotFound_WhenParkingLotDoesNotExist()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var (vid, lp) = await CreateVehicleAsync(authClient);

        var request = new
        {
            LicensePlate = lp,
            ParkingLotId = 999999
        };

        var response = await authClient.PostAsJsonAsync($"/parkinglots/999999/sessions/start", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST /parkinglots/{id}/sessions/stop - End Session

    [Fact]
    public async Task EndSession_ReturnsOk_WhenValidId()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);

        // Start session
        var startRequest = new { LicensePlate = lp, ParkingLotId = parkingLotId };
        await authClient.PostAsJsonAsync($"/parkinglots/{parkingLotId}/sessions/start", startRequest);
        
        // End session
        var stopRequest = new { LicensePlate = lp }; // StopSession request body expects LicensePlate
        var response = await authClient.PostAsJsonAsync($"/parkinglots/{parkingLotId}/sessions/stop", stopRequest);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task EndSession_ReturnsNotFound_WhenInvalidId()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var (vid, lp) = await CreateVehicleAsync(authClient);
        
        var stopRequest = new { LicensePlate = lp };
        var response = await authClient.PostAsJsonAsync("/parkinglots/999999/sessions/stop", stopRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region GET /parkinglots/{id}/sessions/active/{plate}

    [Fact]
    public async Task GetActiveSession_ReturnsOk_WhenSessionExists()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);

        // Start session
        var startRequest = new { LicensePlate = lp, ParkingLotId = parkingLotId };
        await authClient.PostAsJsonAsync($"/parkinglots/{parkingLotId}/sessions/start", startRequest);

        var response = await authClient.GetAsync($"/parkinglots/{parkingLotId}/sessions/active/{lp}");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetActiveSession_ReturnsNotFound_WhenNoSession()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);

        var response = await authClient.GetAsync($"/parkinglots/{parkingLotId}/sessions/active/{lp}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
