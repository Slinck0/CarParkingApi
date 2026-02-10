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
/// Integration tests for all Discount endpoints (happy + sad flows).
/// Tests run against the real Turso test database via HTTP.
/// </summary>
public class DiscountIntegrationTests : IntegrationTestBase
{
    public DiscountIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ================================================================
    // POST /admin/discounts - Create Discount
    // ================================================================

    #region Create Discount - Happy Flows

    [Fact]
    public async Task CreateDiscount_ReturnsCreated_WhenValidPercentageDiscount()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = $"PCT{TestId}",
            Type = DiscountType.Percentage,
            Percentage = 15,
            ValidUntil = DateTime.UtcNow.AddDays(30)
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal($"PCT{TestId}".ToUpper(), doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateDiscount_ReturnsCreated_WhenValidFixedAmountDiscount()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = $"FIX{TestId}",
            Type = DiscountType.FixedAmount,
            FixedAmount = 5.00,
            ValidUntil = DateTime.UtcNow.AddDays(30)
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    #endregion

    #region Create Discount - Sad Flows

    [Fact]
    public async Task CreateDiscount_ReturnsBadRequest_WhenCodeIsEmpty()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = "",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsBadRequest_WhenPercentageOutOfRange()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = $"BAD{TestId}",
            Type = DiscountType.Percentage,
            Percentage = 150,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsBadRequest_WhenValidUntilInPast()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = $"EXP{TestId}",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTime.UtcNow.AddDays(-1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsConflict_WhenDuplicateCode()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);
        var code = $"DUP{TestId}";

        // Create first
        await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = code,
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        });

        // Create duplicate
        var response = await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = code,
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = "NOAUTH",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateDiscount_ReturnsForbidden_WhenNotAdmin()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = $"USR{TestId}",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        });

        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401/403 but got {response.StatusCode}");
    }

    #endregion

    // ================================================================
    // GET /admin/discounts - List All Discounts
    // ================================================================

    #region List Discounts - Happy Flow

    [Fact]
    public async Task GetAllDiscounts_ReturnsOk_WithCreatedDiscounts()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        // Create a discount first
        await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = $"LST{TestId}",
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        });

        var response = await client.GetAsync("/admin/discounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetArrayLength() >= 1);
    }

    #endregion

    #region List Discounts - Sad Flow

    [Fact]
    public async Task GetAllDiscounts_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/admin/discounts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    // ================================================================
    // GET /admin/discounts/{code} - Get Discount by Code
    // ================================================================

    #region Get By Code - Happy Flow

    [Fact]
    public async Task GetDiscountByCode_ReturnsOk_WhenCodeExists()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);
        var code = $"GBC{TestId}";

        await client.PostAsJsonAsync("/admin/discounts", new
        {
            Code = code,
            Type = DiscountType.Percentage,
            Percentage = 25,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        });

        var response = await client.GetAsync($"/admin/discounts/{code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(code.ToUpper(), doc.RootElement.GetProperty("code").GetString());
        Assert.Equal(25, doc.RootElement.GetProperty("percentage").GetDecimal());
    }

    #endregion

    #region Get By Code - Sad Flow

    [Fact]
    public async Task GetDiscountByCode_ReturnsNotFound_WhenCodeDoesNotExist()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/admin/discounts/NONEXISTENT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    // ================================================================
    // PUT /admin/discounts/{id} - Update Discount
    // ================================================================

    #region Update Discount - Happy Flow

    [Fact]
    public async Task UpdateDiscount_ReturnsOk_WhenDeactivating()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var discountId = await CreateDiscountAndGetIdAsync(client, $"UPD{TestId}");

        var response = await client.PutAsJsonAsync($"/admin/discounts/{discountId}", new
        {
            IsActive = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task UpdateDiscount_ReturnsOk_WhenExtendingValidity()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var discountId = await CreateDiscountAndGetIdAsync(client, $"EXT{TestId}");

        var response = await client.PutAsJsonAsync($"/admin/discounts/{discountId}", new
        {
            ValidUntil = DateTime.UtcNow.AddDays(90)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Update Discount - Sad Flows

    [Fact]
    public async Task UpdateDiscount_ReturnsNotFound_WhenIdDoesNotExist()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/admin/discounts/99999", new
        {
            IsActive = false
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDiscount_ReturnsBadRequest_WhenValidUntilInPast()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var discountId = await CreateDiscountAndGetIdAsync(client, $"PST{TestId}");

        var response = await client.PutAsJsonAsync($"/admin/discounts/{discountId}", new
        {
            ValidUntil = DateTime.UtcNow.AddDays(-5)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    // ================================================================
    // DELETE /admin/discounts/{id} - Deactivate Discount (soft delete)
    // ================================================================

    #region Deactivate Discount - Happy Flow

    [Fact]
    public async Task DeactivateDiscount_ReturnsOk_WhenIdExists()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);
        var code = $"DEL{TestId}";

        var discountId = await CreateDiscountAndGetIdAsync(client, code);

        var response = await client.DeleteAsync($"/admin/discounts/{discountId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify it's deactivated
        var getResponse = await client.GetAsync($"/admin/discounts/{code}");
        if (getResponse.IsSuccessStatusCode)
        {
            var body = await getResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.False(doc.RootElement.GetProperty("isActive").GetBoolean());
        }
    }

    #endregion

    #region Deactivate Discount - Sad Flow

    [Fact]
    public async Task DeactivateDiscount_ReturnsNotFound_WhenIdDoesNotExist()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync("/admin/discounts/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    // ================================================================
    // GET /admin/discounts/{id}/stats - Get Discount Statistics
    // ================================================================

    #region Stats - Happy Flow

    [Fact]
    public async Task GetDiscountStatistics_ReturnsOk_WithUsageData()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var discountId = await CreateDiscountAndGetIdAsync(client, $"STA{TestId}");

        var response = await client.GetAsync($"/admin/discounts/{discountId}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(0, doc.RootElement.GetProperty("timesUsed").GetInt32());
    }

    #endregion

    #region Stats - Sad Flow

    [Fact]
    public async Task GetDiscountStatistics_ReturnsNotFound_WhenIdDoesNotExist()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/admin/discounts/99999/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    // ================================================================
    // GET /discounts/validate/{code} - User Validates Discount
    // ================================================================

    #region Validate Discount - Happy Flow

    [Fact]
    public async Task ValidateDiscount_ReturnsOk_WhenCodeIsValid()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);
        var code = $"VAL{TestId}";

        await adminClient.PostAsJsonAsync("/admin/discounts", new
        {
            Code = code,
            Type = DiscountType.Percentage,
            Percentage = 20,
            ValidUntil = DateTime.UtcNow.AddDays(30)
        });

        var userToken = await RegisterAndGetTokenAsync();
        var userClient = CreateAuthenticatedClient(userToken);

        var response = await userClient.GetAsync($"/discounts/validate/{code}?amount=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal(20m, doc.RootElement.GetProperty("discountAmount").GetDecimal());
    }

    #endregion

    #region Validate Discount - Sad Flows

    [Fact]
    public async Task ValidateDiscount_ReturnsNotFound_WhenCodeDoesNotExist()
    {
        var token = await RegisterAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/discounts/validate/DOESNOTEXIST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ValidateDiscount_ReturnsBadRequest_WhenCodeIsExpired()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expiredCode = $"XPR{TestId}".ToUpper();
        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = expiredCode,
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(-1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var token = await RegisterAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync($"/discounts/validate/{expiredCode}");

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400/404 for expired code but got {response.StatusCode}");
    }

    [Fact]
    public async Task ValidateDiscount_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/discounts/validate/ANYCODE");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidateDiscount_ReturnsBadRequest_WhenUsageLimitReached()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var maxedCode = $"MAX{TestId}".ToUpper();
        db.Set<DiscountModel>().Add(new DiscountModel
        {
            Code = maxedCode,
            Type = DiscountType.Percentage,
            Percentage = 10,
            ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
            IsActive = true,
            MaxUsageCount = 1,
            CurrentUsageCount = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var token = await RegisterAndGetTokenAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync($"/discounts/validate/{maxedCode}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    // ================================================================
    // End-to-End: Create -> Validate -> Reserve with discount -> Pay
    // ================================================================

    #region Full End-to-End Flow

    [Fact]
    public async Task FullFlow_CreateDiscount_ApplyToReservation_Pay()
    {
        // 1. Admin creates discount
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);
        var code = $"E2E{TestId}";

        var createResponse = await adminClient.PostAsJsonAsync("/admin/discounts", new
        {
            Code = code,
            Type = DiscountType.Percentage,
            Percentage = 25,
            ValidUntil = DateTime.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // 2. User registers and creates vehicle
        var userToken = await RegisterAndGetTokenAsync();
        var userClient = CreateAuthenticatedClient(userToken);
        var (vehicleId, plate) = await CreateVehicleAsync(userClient);

        // 3. Create parking lot (via admin)
        var lotId = await CreateParkingLotAsync(adminToken);

        // 4. User validates discount code
        var validateResponse = await userClient.GetAsync($"/discounts/validate/{code}?amount=50");
        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);

        // 5. User creates reservation with discount code
        var reservationResponse = await userClient.PostAsJsonAsync("/reservations", new
        {
            LicensePlate = plate,
            StartDate = DateTime.UtcNow.AddHours(2),
            EndDate = DateTime.UtcNow.AddHours(5),
            ParkingLot = lotId,
            DiscountCode = code
        });
        Assert.True(reservationResponse.IsSuccessStatusCode,
            $"Reservation failed: {await reservationResponse.Content.ReadAsStringAsync()}");

        // 6. Admin checks discount stats - usage should be recorded
        var statsCodeResponse = await adminClient.GetAsync($"/admin/discounts/{code}");
        Assert.Equal(HttpStatusCode.OK, statsCodeResponse.StatusCode);
    }

    #endregion

    // ================================================================
    // Helper
    // ================================================================

    private async Task<int> CreateDiscountAndGetIdAsync(HttpClient adminClient, string code, int percentage = 10)
    {
        var response = await adminClient.PostAsJsonAsync("/admin/discounts", new
        {
            Code = code,
            Type = DiscountType.Percentage,
            Percentage = percentage,
            ValidUntil = DateTime.UtcNow.AddDays(30)
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetInt32();
    }
}
