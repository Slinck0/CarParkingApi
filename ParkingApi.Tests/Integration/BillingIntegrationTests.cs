using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Integration tests for Billing endpoints using real Turso database.
/// </summary>
public class BillingIntegrationTests : IntegrationTestBase
{
    public BillingIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region GET /billing - Get Upcoming Payments

    [Fact]
    public async Task GetUpcomingPayments_ReturnsOk_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/billing");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUpcomingPayments_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/billing");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /billing/history - Get Billing History

    [Fact]
    public async Task GetBillingHistory_ReturnsOk_WhenAuthenticated()
    {
        var token = await RegisterAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/billing/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBillingHistory_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/billing/history");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
