using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using V2.Data;
using V2.Models;
using Xunit;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Integration tests for all API endpoints.
/// Uses WebApplicationFactory to spin up the actual API and make real HTTP requests.
/// </summary>
public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Helper Methods

    private async Task<string> RegisterAndGetTokenAsync(string? username = null)
    {
        username ??= $"user_{Guid.NewGuid():N}"[..20];
        var password = "TestPass123";
        
        var registerRequest = new
        {
            Username = username,
            Password = password,
            Name = "Test User",
            PhoneNumber = "0612345678",
            Email = $"{username}@test.nl",
            BirthYear = 1990
        };

        await _client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/login", loginRequest);
        
        if (!loginResponse.IsSuccessStatusCode)
            return string.Empty;

        var content = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("token").GetString() ?? string.Empty;
    }

    private async Task<string> CreateAdminAndGetTokenAsync()
    {
        // Create admin directly in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var adminUsername = $"admin_{Guid.NewGuid():N}"[..20];
        var adminUser = new UserModel
        {
            Username = adminUsername,
            Password = BCrypt.Net.BCrypt.HashPassword("AdminPass123"),
            Name = "Test Admin",
            Email = $"{adminUsername}@test.nl",
            Phone = "0612345679",
            Role = "ADMIN",
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now),
            BirthYear = 1985
        };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        // Login
        var loginRequest = new { Username = adminUsername, Password = "AdminPass123" };
        var loginResponse = await _client.PostAsJsonAsync("/login", loginRequest);
        
        var content = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("token").GetString() ?? string.Empty;
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    #endregion

    #region Health Endpoint

    [Fact]
    public async Task Health_ReturnsOk_WithMessage()
    {
        var response = await _client.GetAsync("/Health");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Parking API is running", content);
    }

    #endregion

    #region Register Endpoint

    [Fact]
    public async Task Register_ReturnsCreated_WhenDataIsValid()
    {
        var request = new
        {
            Username = $"newuser_{Guid.NewGuid():N}"[..20],
            Password = "ValidPass123",
            Name = "New User",
            PhoneNumber = "0612345678",
            Email = $"newuser_{Guid.NewGuid():N}@test.nl",
            BirthYear = 1990
        };

        var response = await _client.PostAsJsonAsync("/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        var request = new
        {
            Username = $"baduser_{Guid.NewGuid():N}"[..20],
            Password = "ValidPass123",
            Name = "Bad Email User",
            PhoneNumber = "0612345678",
            Email = "invalid-email",
            BirthYear = 1990
        };

        var response = await _client.PostAsJsonAsync("/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenUsernameExists()
    {
        var username = $"dupuser_{Guid.NewGuid():N}"[..20];
        
        // First registration
        var request1 = new
        {
            Username = username,
            Password = "ValidPass123",
            Name = "First User",
            PhoneNumber = "0612345678",
            Email = $"first_{Guid.NewGuid():N}@test.nl",
            BirthYear = 1990
        };
        await _client.PostAsJsonAsync("/register", request1);

        // Second registration with same username
        var request2 = new
        {
            Username = username,
            Password = "ValidPass123",
            Name = "Second User",
            PhoneNumber = "0612345678",
            Email = $"second_{Guid.NewGuid():N}@test.nl",
            BirthYear = 1990
        };
        var response = await _client.PostAsJsonAsync("/register", request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    #endregion

    #region Login Endpoint

    [Fact]
    public async Task Login_ReturnsOk_WithToken_WhenCredentialsAreValid()
    {
        var username = $"loginuser_{Guid.NewGuid():N}"[..20];
        var password = "LoginPass123";
        
        // Register first
        await _client.PostAsJsonAsync("/register", new
        {
            Username = username,
            Password = password,
            Name = "Login User",
            PhoneNumber = "0612345678",
            Email = $"{username}@test.nl",
            BirthYear = 1990
        });

        // Login
        var response = await _client.PostAsJsonAsync("/login", new { Username = username, Password = password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", content);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsWrong()
    {
        var username = $"wrongpass_{Guid.NewGuid():N}"[..20];
        
        await _client.PostAsJsonAsync("/register", new
        {
            Username = username,
            Password = "CorrectPass123",
            Name = "Wrong Pass User",
            PhoneNumber = "0612345678",
            Email = $"{username}@test.nl",
            BirthYear = 1990
        });

        var response = await _client.PostAsJsonAsync("/login", new { Username = username, Password = "WrongPass123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        var response = await _client.PostAsJsonAsync("/login", new { Username = "", Password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Profile Endpoints

    [Fact]
    public async Task GetProfile_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_ReturnsOk_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsOk_WhenDataIsValid()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var updateRequest = new
        {
            Name = "Updated Name",
            Email = $"updated_{Guid.NewGuid():N}@test.nl",
            PhoneNumber = "0699999999",
            BirthYear = 1995
        };

        var response = await authClient.PutAsJsonAsync("/profile", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProfile_ReturnsOk_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.DeleteAsync("/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Vehicle Endpoints

    [Fact]
    public async Task CreateVehicle_ReturnsCreated_WhenDataIsValid()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var vehicleRequest = new
        {
            LicensePlate = $"AA-{Guid.NewGuid():N}"[..10],
            Make = "Tesla",
            Model = "Model 3",
            Color = "Black"
        };

        var response = await authClient.PostAsJsonAsync("/vehicles", vehicleRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetMyVehicles_ReturnsOk_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/vehicles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateVehicle_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var vehicleRequest = new
        {
            LicensePlate = "XX-999-XX",
            Make = "Ford",
            Model = "Focus",
            Color = "Blue"
        };

        var response = await _client.PostAsJsonAsync("/vehicles", vehicleRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Reservation Endpoints

    [Fact]
    public async Task CreateReservation_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var reservationRequest = new
        {
            LicensePlate = "AA-BB-12",
            StartDate = DateTime.UtcNow.AddHours(1),
            EndDate = DateTime.UtcNow.AddHours(2),
            ParkingLot = 1
        };

        var response = await _client.PostAsJsonAsync("/reservations", reservationRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyReservations_ReturnsOk_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/reservations/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Session Endpoints

    [Fact]
    public async Task StartSession_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var sessionRequest = new { LicensePlate = "AA-BB-99" };

        var response = await _client.PostAsJsonAsync("/parkinglots/1/sessions/start", sessionRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StopSession_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var sessionRequest = new { LicensePlate = "AA-BB-99" };

        var response = await _client.PostAsJsonAsync("/parkinglots/1/sessions/stop", sessionRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Payment Endpoints

    [Fact]
    public async Task CreatePayment_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var paymentRequest = new { ReservationId = "res-123", Method = "CreditCard" };

        var response = await _client.PostAsJsonAsync("/payments/", paymentRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserPayments_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/payments/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserPayments_ReturnsOk_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/payments/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPendingPayments_ReturnsOk_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/payments/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Admin Endpoints - Organization

    [Fact]
    public async Task CreateOrganization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var request = new { Name = "New Organization", Email = "org@test.nl" };

        var response = await _client.PostAsJsonAsync("/admin/organizations", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_ReturnsForbidden_WhenNotAdmin()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var request = new { Name = "New Organization", Email = "org@test.nl" };

        var response = await authClient.PostAsJsonAsync("/admin/organizations", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrganization_ReturnsCreated_WhenAdmin()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var request = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };

        var response = await authClient.PostAsJsonAsync("/admin/organizations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    #endregion

    #region Admin Endpoints - Parking Lots

    [Fact]
    public async Task CreateParkingLot_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var parkingLotRequest = new
        {
            Name = "New Garage",
            Location = "Rotterdam",
            Address = "Coolsingel 1",
            Capacity = 100,
            Reserved = 0,
            Tariff = 5.0m,
            DayTariff = 25.0m,
            Lat = 51.9225,
            Lng = 4.47917,
            Status = "Open"
        };

        var response = await _client.PostAsJsonAsync("/parking-lots", parkingLotRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateParkingLot_ReturnsForbidden_WhenNotAdmin()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var parkingLotRequest = new
        {
            Name = "New Garage",
            Location = "Rotterdam",
            Address = "Coolsingel 1",
            Capacity = 100,
            Reserved = 0,
            Tariff = 5.0m,
            DayTariff = 25.0m,
            Lat = 51.9225,
            Lng = 4.47917,
            Status = "Open"
        };

        var response = await authClient.PostAsJsonAsync("/parking-lots", parkingLotRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Admin Endpoints - User Toggle Active

    [Fact]
    public async Task ToggleUserActive_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.PutAsync("/admin/users/1/toggle-active", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ToggleUserActive_ReturnsForbidden_WhenNotAdmin()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.PutAsync("/admin/users/1/toggle-active", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Admin Endpoints - Payments

    [Fact]
    public async Task AdminCancelPayment_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.PutAsync("/admin/payments/transaction123/cancel", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdatePayment_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var request = new { Amount = 50.0m, Method = "Ideal" };

        var response = await _client.PutAsJsonAsync("/admin/payments/transaction123", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
