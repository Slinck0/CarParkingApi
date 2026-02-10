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
/// Integration tests for Payment endpoints using real Turso database.
/// </summary>
public class PaymentIntegrationTests : IntegrationTestBase
{
    public PaymentIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region POST /payments - Process Payment

    [Fact]
    public async Task CreatePayment_ReturnsCreated_WhenValidData()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);
        
        // Ensure a reservation exists to pay for (if payment logic requires it)
        // Previous test created a reservation. Let's do that to be safe.
        var reservationId = await CreateReservationAsync(authClient, parkingLotId, lp);

        var paymentRequest = new
        {
            ReservationId = reservationId,
            Method = "CreditCard"
        };

        var response = await authClient.PostAsJsonAsync("/payments", paymentRequest);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task CreatePayment_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var paymentRequest = new
        {
            Amount = 10.00m,
            PaymentMethod = "CreditCard",
            Status = "Pending",
            Timestamp = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/payments", paymentRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task CreatePayment_ReturnsBadRequest_WhenInvalidData()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var paymentRequest = new
        {
             ReservationId = "",  // Invalid/empty reservation ID
             Method = ""
        };

        var response = await authClient.PostAsJsonAsync("/payments", paymentRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region GET /payments - Get User Completed Payments

    [Fact]
    public async Task GetUserPayments_ReturnsOk_AndListsCompletedPayments()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid, lp) = await CreateVehicleAsync(authClient);
        var reservationId = await CreateReservationAsync(authClient, parkingLotId, lp);

        // Create a completed payment
        var paymentRequest = new
        {
            ReservationId = reservationId,
            Method = "CreditCard"
        };
        var createResponse = await authClient.PostAsJsonAsync("/payments", paymentRequest);
        Assert.True(createResponse.IsSuccessStatusCode, "Payment creation should succeed");

        // Get completed payments
        var response = await authClient.GetAsync("/payments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var payments = doc.RootElement.EnumerateArray().ToList();

        // Should have at least one completed payment
        Assert.NotEmpty(payments);

        // Verify payment has required fields
        var firstPayment = payments.First();
        Assert.True(firstPayment.TryGetProperty("transaction", out _), "Payment should have transaction ID");
        Assert.True(firstPayment.TryGetProperty("amount", out _), "Payment should have amount");
        Assert.True(firstPayment.TryGetProperty("method", out _), "Payment should have method");
        Assert.True(firstPayment.TryGetProperty("status", out _), "Payment should have status");
        Assert.True(firstPayment.TryGetProperty("reservationId", out _), "Payment should have reservationId");
    }

    [Fact]
    public async Task GetUserPayments_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/payments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserPayments_ReturnsOnlyMyPayments()
    {
        // Create first user with payment
        var token1 = await RegisterAndGetTokenAsync();
        var authClient1 = CreateAuthenticatedClient(token1);
        var parkingLotId = await CreateParkingLotAsync();
        var (vid1, lp1) = await CreateVehicleAsync(authClient1);
        var reservationId1 = await CreateReservationAsync(authClient1, parkingLotId, lp1);
        await authClient1.PostAsJsonAsync("/payments", new { ReservationId = reservationId1, Method = "CreditCard" });

        // Create second user with payment
        var token2 = await RegisterAndGetTokenAsync();
        var authClient2 = CreateAuthenticatedClient(token2);
        var (vid2, lp2) = await CreateVehicleAsync(authClient2);
        var reservationId2 = await CreateReservationAsync(authClient2, parkingLotId, lp2);
        await authClient2.PostAsJsonAsync("/payments", new { ReservationId = reservationId2, Method = "BankTransfer" });

        // User 1 should only see their own payment
        var response1 = await authClient1.GetAsync("/payments");
        var content1 = await response1.Content.ReadAsStringAsync();
        using var doc1 = JsonDocument.Parse(content1);
        var payments1 = doc1.RootElement.EnumerateArray().ToList();
        Assert.Single(payments1);
        Assert.Equal("CreditCard", payments1[0].GetProperty("method").GetString());

        // User 2 should only see their own payment
        var response2 = await authClient2.GetAsync("/payments");
        var content2 = await response2.Content.ReadAsStringAsync();
        using var doc2 = JsonDocument.Parse(content2);
        var payments2 = doc2.RootElement.EnumerateArray().ToList();
        Assert.Single(payments2);
        Assert.Equal("BankTransfer", payments2[0].GetProperty("method").GetString());
    }

    #endregion

    #region GET /payments/pending - Get User Pending Payments

    [Fact]
    public async Task GetPendingPayments_ReturnsOk()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/payments/pending");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var payments = doc.RootElement.EnumerateArray().ToList();
        // Should return empty list or list of pending payments
        Assert.NotNull(payments);
    }

    [Fact]
    public async Task GetPendingPayments_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/payments/pending");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
