using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ParkingApi.Tests.Helpers;
using V2.Data;
using V2.Models;
using Xunit;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Base class for integration tests using Dedicated Test Database.
/// Ensures database is wiped after each test for full isolation.
/// </summary>
[Collection("IntegrationTests")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory _factory;
    protected readonly HttpClient _client;
    
    // Unique identifier for this test instance - enables parallel execution
    protected readonly string TestId = Guid.NewGuid().ToString("N")[..8];

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Runs after EACH test.
    /// Wipes the entire test database for complete test isolation.
    /// </summary>
    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await WipeDatabase(db);
    }

    private async Task WipeDatabase(AppDbContext db)
    {
        // Delete ALL test data in dependency order (children first)
        await db.Database.ExecuteSqlRawAsync("DELETE FROM discount_usage");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM payment");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM parking_sessions");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM reservation");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM vehicle");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM discount");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM parking_lot");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM user");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM organization");
    }

    // --- Helper Methods (No Tracking/Prefixes needed anymore, but keeping generation logic) ---

    // [Removes tracking calls from helpers]

    protected async Task<string> RegisterAndGetTokenAsync(string? username = null)
    {
        // Use TestId prefix for namespace isolation
        username ??= $"test_{TestId}_{Guid.NewGuid():N}"[..20];
        var password = "TestPass123";
        
        var registerRequest = new
        {
            Username = username,
            Password = password,
            Name = "Test User",
            PhoneNumber = "0612345678",
            Email = $"{username.Replace("_", "")}@test.nl",
            BirthYear = 1990
        };

        var regResponse = await _client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/login", loginRequest);
        
        if (!loginResponse.IsSuccessStatusCode)
            return string.Empty;

        var content = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("token").GetString() ?? string.Empty;
    }

    protected HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected async Task<(int id, string plate)> CreateVehicleAsync(HttpClient authClient)
    {
        var g = Guid.NewGuid().ToString("N").ToUpper();
        var uniquePlate = $"{g.Substring(0, 2)}-{g.Substring(2, 2)}-{g.Substring(4, 2)}";

        var vehicleRequest = new
        {
            LicensePlate = uniquePlate,
            Make = "Tesla",
            Model = "Model 3",
            Color = "Black",
            Year = 2023
        };

        var response = await authClient.PostAsJsonAsync("/vehicles", vehicleRequest);
        
        var location = response.Headers.Location?.ToString();
        var idString = location?.Split('/').Last();
        
        if (int.TryParse(idString, out int id))
        {
            return (id, uniquePlate);
        }
        
        try 
        {
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
            {
                id = idProp.GetInt32();
                return (id, uniquePlate);
            }
        }
        catch { }

        throw new KeyNotFoundException($"Could not extract ID from response. Location: {location}");
    }

    protected async Task<string> CreateAdminAndGetTokenAsync()
    {
        // Use TestId prefix for namespace isolation
        var username = $"admin_{TestId}_{Guid.NewGuid():N}"[..20];
        var password = "AdminPass123";
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var adminUser = new UserModel
        {
            Username = username,
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Name = "Test Admin",
            Email = $"{username.Replace("_", "")}@test.nl",
            Phone = "0612345679",
            Role = "ADMIN",
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now),
            BirthYear = 1985
        };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        var loginRequest = new { Username = username, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/login", loginRequest);
        
        if (!loginResponse.IsSuccessStatusCode)
            return string.Empty;

        var content = await loginResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("token").GetString() ?? string.Empty;
    }

    protected async Task<int> CreateParkingLotAsync(string? adminToken = null)
    {
        if (adminToken == null) adminToken = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(adminToken);

        var createRequest = new ParkingLotCreate(
            $"TestLot_{Guid.NewGuid():N}"[..30],
            "Rotterdam",
            "Test Street 1",
            100,
            0,
            5.0m,
            25.0m,
            51.9225,
            4.47917,
            "Open",
            null,
            null
        );

        var response = await client.PostAsJsonAsync("/parking-lots", createRequest);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        if (doc.RootElement.TryGetProperty("id", out var idProp))
        {
            return idProp.GetInt32();
        }
        
        if (doc.RootElement.TryGetProperty("parkingLotId", out var plIdProp))
        {
            return plIdProp.GetInt32();
        }

        throw new JsonException($"Could not find ID in response: {content}");
    }

    protected async Task<string> CreateReservationAsync(HttpClient authClient, int parkingLotId, string licensePlate, DateTime? start = null, DateTime? end = null)
    {
        var request = new
        {
            LicensePlate = licensePlate,
            StartDate = start ?? DateTime.UtcNow.AddHours(1),
            EndDate = end ?? DateTime.UtcNow.AddHours(3),
            ParkingLot = parkingLotId
        };

        var response = await authClient.PostAsJsonAsync("/reservations", request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        
        if (doc.RootElement.TryGetProperty("reservation", out var resProp) && resProp.TryGetProperty("id", out var idProp))
        {
            return idProp.GetString()!;
        }
        
        if (doc.RootElement.TryGetProperty("id", out var directIdProp))
        {
            return directIdProp.GetString()!;
        }

        throw new JsonException($"Could not find Reservation ID in response: {content}");
    }

    protected async Task<int> CreateDiscountAsync(HttpClient adminClient, string code, int percentage = 10)
    {
        var discountRequest = new
        {
            Code = code,
            Description = "Test Discount",
            Type = DiscountType.Percentage,
            Percentage = percentage,
            ValidUntil = DateTime.UtcNow.AddDays(30)
        };

        var response = await adminClient.PostAsJsonAsync("/admin/discounts", discountRequest);
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var discount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Discounts, d => d.Code == code.ToUpperInvariant());
        if (discount != null)
        {
            return discount.Id;
        }

        throw new Exception($"Discount with code '{code}' was created but could not be found in DB.");
    }

    // Stub method to fix valid build references (if any leftovers exist)
    // Deprecated but kept to prevent build breakage during transition
    protected void TrackUser(int id) { }
    protected void TrackParkingLot(int id) { }
    protected void TrackVehicle(int id) { }
    protected void TrackReservation(string id) { }
    protected void TrackSession(int id) { }
    protected void TrackPayment(string id) { }
    protected void TrackDiscount(int id) { }
    protected void TrackDiscountUsage(int id) { }
}
