using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using V2.Helpers;
using V2.Models;

namespace ParkingApi.Tests.Handlers;

public class OrganizationHandlersTests
{
    private static object? GetValue(object result)
        => result.GetType().GetProperty("Value")?.GetValue(result);

    [Fact]
    public async Task CreateOrganization_ReturnsCreated_WhenValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var req = new OrganizationCreateRequest(
            Name: "  Acme  ",
            Email: " info@acme.com ",
            Phone: " 123 ",
            Address: " Street 1 ",
            City: " Amsterdam ",
            Country: " NL "
        );

        var result = await OrganizationHandlers.CreateOrganization(db, req);

        var sc = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status201Created, sc.StatusCode);

        Assert.Equal(1, await db.Organizations.CountAsync());
        var saved = await db.Organizations.FirstAsync();
        Assert.Equal("Acme", saved.Name);
        Assert.Equal("info@acme.com", saved.Email);
        Assert.Equal("123", saved.Phone);
        Assert.Equal("Street 1", saved.Address);
        Assert.Equal("Amsterdam", saved.City);
        Assert.Equal("NL", saved.Country);
    }

    [Fact]
    public async Task CreateOrganization_ReturnsBadRequest_WhenNameMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var req = new OrganizationCreateRequest(
            Name: "   ",
            Email: null,
            Phone: null,
            Address: null,
            City: null,
            Country: null
        );

        var result = await OrganizationHandlers.CreateOrganization(db, req);

        Assert.IsType<BadRequest<string>>(result);
        Assert.Equal(0, await db.Organizations.CountAsync());
    }

    [Fact]
    public async Task CreateOrganization_ReturnsConflict_WhenDuplicateName_CaseInsensitive()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Name = "Acme" });
        await db.SaveChangesAsync();

        var req = new OrganizationCreateRequest("acme", null, null, null, null, null);
        var result = await OrganizationHandlers.CreateOrganization(db, req);

        Assert.IsType<Conflict<string>>(result);
        Assert.Equal(1, await db.Organizations.CountAsync());
    }

    [Fact]
    public async Task GetOrganizationById_ReturnsOk_WhenExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org1" });
        await db.SaveChangesAsync();

        var result = await OrganizationHandlers.GetOrganizationById(1, db);

        var sc = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, sc.StatusCode);

        var value = GetValue(result);
        Assert.NotNull(value);
        var name = value!.GetType().GetProperty("Name")?.GetValue(value) as string;
        Assert.Equal("Org1", name);
    }

    [Fact]
    public async Task GetOrganizationById_ReturnsNotFound_WhenMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var result = await OrganizationHandlers.GetOrganizationById(999, db);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task UpdateOrganization_ReturnsOk_WhenValid_AndSameName_NoUniqCheck()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Acme", Email = "old@acme.com" });
        db.Organizations.Add(new OrganizationModel { Id = 2, Name = "Other" });
        await db.SaveChangesAsync();

        var req = new OrganizationUpdateRequest(
            Name: "ACME",
            Email: "new@acme.com",
            Phone: null,
            Address: null,
            City: null,
            Country: null
        );

        var result = await OrganizationHandlers.UpdateOrganization(1, db, req);

        var sc = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, sc.StatusCode);

        var saved = await db.Organizations.FirstAsync(o => o.Id == 1);
        Assert.Equal("ACME", saved.Name);
        Assert.Equal("new@acme.com", saved.Email);
    }

    [Fact]
    public async Task UpdateOrganization_ReturnsConflict_WhenRenamingToExistingName()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "OrgA" });
        db.Organizations.Add(new OrganizationModel { Id = 2, Name = "OrgB" });
        await db.SaveChangesAsync();

        var req = new OrganizationUpdateRequest("OrgB", null, null, null, null, null);
        var result = await OrganizationHandlers.UpdateOrganization(1, db, req);

        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public async Task UpdateOrganization_ReturnsBadRequest_WhenNameMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "OrgA" });
        await db.SaveChangesAsync();

        var req = new OrganizationUpdateRequest("   ", null, null, null, null, null);
        var result = await OrganizationHandlers.UpdateOrganization(1, db, req);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task UpdateOrganization_ReturnsNotFound_WhenMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var req = new OrganizationUpdateRequest("OrgX", null, null, null, null, null);
        var result = await OrganizationHandlers.UpdateOrganization(999, db, req);

        Assert.IsType<NotFound<string>>(result);
    }

    [Fact]
    public async Task DeleteOrganization_ReturnsNoContent_WhenExists()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "OrgA" });
        await db.SaveChangesAsync();

        var result = await OrganizationHandlers.DeleteOrganization(1, db);

        Assert.IsType<NoContent>(result);
        Assert.Equal(0, await db.Organizations.CountAsync());
    }

    [Fact]
    public async Task DeleteOrganization_ReturnsNotFound_WhenMissing()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var result = await OrganizationHandlers.DeleteOrganization(999, db);

        Assert.IsType<NotFound<string>>(result);
    }
}
