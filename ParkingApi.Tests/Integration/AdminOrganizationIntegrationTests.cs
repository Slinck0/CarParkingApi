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
/// Integration tests for Admin Organization endpoints using real Turso database.
/// </summary>
public class AdminOrganizationIntegrationTests : IntegrationTestBase
{
    public AdminOrganizationIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // Helpers inherited from IntegrationTestBase

    private async Task<int> GetUserIdAsync(string token)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Extract username from token or use a different approach
        var authClient = CreateAuthenticatedClient(token);
        var profileResponse = await authClient.GetAsync("/profile");
        var profileContent = await profileResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(profileContent);
        return doc.RootElement.GetProperty("id").GetInt32();
    }



    #region GET /admin/organizations/{id} - Get Organization By ID

    [Fact]
    public async Task GetOrganizationById_ReturnsOk_WhenIdExists()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        // Create organization first
        var createRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createResponse = await authClient.PostAsJsonAsync("/admin/organizations", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createContent);
        var orgId = doc.RootElement.GetProperty("id").GetInt32();

        // Get organization
        var response = await authClient.GetAsync($"/admin/organizations/{orgId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationById_ReturnsNotFound_WhenIdDoesNotExist()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/admin/organizations/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationById_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/admin/organizations/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region PUT /admin/organizations/{id} - Update Organization

    [Fact]
    public async Task UpdateOrganization_ReturnsOk_WhenValidUpdate()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        // Create organization first
        var createRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createResponse = await authClient.PostAsJsonAsync("/admin/organizations", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createContent);
        var orgId = doc.RootElement.GetProperty("id").GetInt32();

        // Update organization
        var updateRequest = new
        {
            Name = $"Updated_{Guid.NewGuid():N}"[..20]
        };

        var response = await authClient.PutAsJsonAsync($"/admin/organizations/{orgId}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrganization_ReturnsNotFound_WhenInvalidId()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var updateRequest = new { Name = "Updated Name" };
        var response = await authClient.PutAsJsonAsync("/admin/organizations/999999", updateRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrganization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var updateRequest = new { Name = "Updated" };
        var response = await _client.PutAsJsonAsync("/admin/organizations/1", updateRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DELETE /admin/organizations/{id} - Delete Organization

    [Fact]
    public async Task DeleteOrganization_ReturnsOk_WhenValidId()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        // Create organization first
        var createRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createResponse = await authClient.PostAsJsonAsync("/admin/organizations", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createContent);
        var orgId = doc.RootElement.GetProperty("id").GetInt32();

        // Delete organization
        var response = await authClient.DeleteAsync($"/admin/organizations/{orgId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrganization_ReturnsNotFound_WhenInvalidId()
    {
        var token = await CreateAdminAndGetTokenAsync();
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.DeleteAsync("/admin/organizations/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrganization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.DeleteAsync("/admin/organizations/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region POST /admin/organizations/{orgId}/users/{userId} - Assign User to Organization

    [Fact]
    public async Task AssignUserToOrganization_ReturnsOk_WhenValidIds()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        // Create organization
        var createOrgRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createOrgResponse = await adminClient.PostAsJsonAsync("/admin/organizations", createOrgRequest);
        var createOrgContent = await createOrgResponse.Content.ReadAsStringAsync();
        using var orgDoc = JsonDocument.Parse(createOrgContent);
        var orgId = orgDoc.RootElement.GetProperty("id").GetInt32();

        // Create user
        var userToken = await RegisterAndGetTokenAsync();
        var userId = await GetUserIdAsync(userToken);

        // Assign user to organization
        var response = await adminClient.PostAsJsonAsync($"/admin/organizations/{orgId}/users/{userId}", new { });
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task AssignUserToOrganization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.PostAsJsonAsync("/admin/organizations/1/users/1", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DELETE /admin/organizations/{orgId}/users/{userId} - Remove User from Organization

    [Fact]
    public async Task RemoveUserFromOrganization_ReturnsOk_WhenUserInOrganization()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        // Create organization
        var createOrgRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createOrgResponse = await adminClient.PostAsJsonAsync("/admin/organizations", createOrgRequest);
        var createOrgContent = await createOrgResponse.Content.ReadAsStringAsync();
        using var orgDoc = JsonDocument.Parse(createOrgContent);
        var orgId = orgDoc.RootElement.GetProperty("id").GetInt32();

        // Create and assign user
        var userToken = await RegisterAndGetTokenAsync();
        var userId = await GetUserIdAsync(userToken);
        await adminClient.PostAsJsonAsync($"/admin/organizations/{orgId}/users/{userId}", new { });

        // Remove user
        var response = await adminClient.DeleteAsync($"/admin/organizations/{orgId}/users/{userId}");
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task RemoveUserFromOrganization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.DeleteAsync("/admin/organizations/1/users/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region PUT /admin/organizations/{orgId}/users/{userId}/role - Update User Role

    [Fact]
    public async Task UpdateUserOrganizationRole_ReturnsOk_WhenValidRole()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        // Create organization
        var createOrgRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createOrgResponse = await adminClient.PostAsJsonAsync("/admin/organizations", createOrgRequest);
        var createOrgContent = await createOrgResponse.Content.ReadAsStringAsync();
        using var orgDoc = JsonDocument.Parse(createOrgContent);
        var orgId = orgDoc.RootElement.GetProperty("id").GetInt32();

        // Create and assign user
        var userToken = await RegisterAndGetTokenAsync();
        var userId = await GetUserIdAsync(userToken);
        await adminClient.PostAsJsonAsync($"/admin/organizations/{orgId}/users/{userId}", new { });

        // Update role
        var updateRequest = new { Role = "Manager" };
        var response = await adminClient.PutAsJsonAsync($"/admin/organizations/{orgId}/users/{userId}/role", updateRequest);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateUserOrganizationRole_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var updateRequest = new { Role = "Manager" };
        var response = await _client.PutAsJsonAsync("/admin/organizations/1/users/1/role", updateRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /admin/organizations/{orgId}/users - Get Organization Users

    [Fact]
    public async Task GetOrganizationUsers_ReturnsOk_WhenValidOrgId()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        // Create organization
        var createOrgRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createOrgResponse = await adminClient.PostAsJsonAsync("/admin/organizations", createOrgRequest);
        var createOrgContent = await createOrgResponse.Content.ReadAsStringAsync();
        using var orgDoc = JsonDocument.Parse(createOrgContent);
        var orgId = orgDoc.RootElement.GetProperty("id").GetInt32();

        // Get users
        var response = await adminClient.GetAsync($"/admin/organizations/{orgId}/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationUsers_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/admin/organizations/1/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region POST /admin/organizations/{orgId}/parking-lots - Create Parking Lot for Organization

    [Fact]
    public async Task CreateParkingLotForOrganization_ReturnsCreated_WhenValidData()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        // Create organization
        var createOrgRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createOrgResponse = await adminClient.PostAsJsonAsync("/admin/organizations", createOrgRequest);
        var createOrgContent = await createOrgResponse.Content.ReadAsStringAsync();
        using var orgDoc = JsonDocument.Parse(createOrgContent);
        var orgId = orgDoc.RootElement.GetProperty("id").GetInt32();

        // Create parking lot for organization
        var createLotRequest = new
        {
            Name = $"OrgLot_{Guid.NewGuid():N}"[..15],
            Location = "Rotterdam",
            Address = "Org Street 1",
            Capacity = 50,
            Tariff = 4.0m,
            DayTariff = 20.0m,
            Lat = 51.9225,
            Lng = 4.47917
        };

        var response = await adminClient.PostAsJsonAsync($"/admin/organizations/{orgId}/parking-lots", createLotRequest);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task CreateParkingLotForOrganization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var createLotRequest = new { Name = "Test Lot" };
        var response = await _client.PostAsJsonAsync("/admin/organizations/1/parking-lots", createLotRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DELETE /admin/organizations/{orgId}/parking-lots/{parkingLotId} - Delete Parking Lot from Organization

    [Fact]
    public async Task DeleteParkingLotFromOrganization_ReturnsOk_WhenValidIds()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        // Create organization
        var createOrgRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createOrgResponse = await adminClient.PostAsJsonAsync("/admin/organizations", createOrgRequest);
        var createOrgContent = await createOrgResponse.Content.ReadAsStringAsync();
        using var orgDoc = JsonDocument.Parse(createOrgContent);
        var orgId = orgDoc.RootElement.GetProperty("id").GetInt32();

        // Create parking lot for organization
        var createLotRequest = new
        {
            Name = $"OrgLot_{Guid.NewGuid():N}"[..15],
            Location = "Rotterdam",
            Address = "Org Street 1",
            Capacity = 50,
            Tariff = 4.0m,
            DayTariff = 20.0m,
            Lat = 51.9225,
            Lng = 4.47917
        };
        var createLotResponse = await adminClient.PostAsJsonAsync($"/admin/organizations/{orgId}/parking-lots", createLotRequest);
        var createLotContent = await createLotResponse.Content.ReadAsStringAsync();
        using var lotDoc = JsonDocument.Parse(createLotContent);
        var lotId = lotDoc.RootElement.GetProperty("id").GetInt32();

        // Delete parking lot
        var response = await adminClient.DeleteAsync($"/admin/organizations/{orgId}/parking-lots/{lotId}");
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}");
    }

    [Fact]
    public async Task DeleteParkingLotFromOrganization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.DeleteAsync("/admin/organizations/1/parking-lots/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /admin/organizations/{orgId}/parking-lots - Get Organization Parking Lots

    [Fact]
    public async Task GetOrganizationParkingLots_ReturnsOk_WhenValidOrgId()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        // Create organization
        var createOrgRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createOrgResponse = await adminClient.PostAsJsonAsync("/admin/organizations", createOrgRequest);
        var createOrgContent = await createOrgResponse.Content.ReadAsStringAsync();
        using var orgDoc = JsonDocument.Parse(createOrgContent);
        var orgId = orgDoc.RootElement.GetProperty("id").GetInt32();

        // Get parking lots
        var response = await adminClient.GetAsync($"/admin/organizations/{orgId}/parking-lots");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationParkingLots_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/admin/organizations/1/parking-lots");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /admin/organizations/{orgId}/vehicles - Get Organization Vehicles

    [Fact]
    public async Task GetOrganizationVehicles_ReturnsOk_WhenValidOrgId()
    {
        var adminToken = await CreateAdminAndGetTokenAsync();
        var adminClient = CreateAuthenticatedClient(adminToken);

        // Create organization
        var createOrgRequest = new
        {
            Name = $"Org_{Guid.NewGuid():N}"[..20],
            Email = $"org_{Guid.NewGuid():N}@test.nl"
        };
        var createOrgResponse = await adminClient.PostAsJsonAsync("/admin/organizations", createOrgRequest);
        var createOrgContent = await createOrgResponse.Content.ReadAsStringAsync();
        using var orgDoc = JsonDocument.Parse(createOrgContent);
        var orgId = orgDoc.RootElement.GetProperty("id").GetInt32();

        // Get vehicles
        var response = await adminClient.GetAsync($"/admin/organizations/{orgId}/vehicles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationVehicles_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/admin/organizations/1/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
