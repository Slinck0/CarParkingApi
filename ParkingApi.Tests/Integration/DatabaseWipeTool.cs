using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using Xunit;

namespace ParkingApi.Tests.Integration;

/// <summary>
/// Utility test to force a database wipe.
/// Run this specifically to clean the environment.
/// </summary>
[Collection("IntegrationTests")]
public class DatabaseWipeTool : IntegrationTestBase
{
    public DatabaseWipeTool(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task WipeDatabase_Force()
    {
        // IntegrationTestBase.DisposeAsync() automatically calls WipeTestDatabase()
        // So just by running this empty test, the teardown will wipe the DB.
        // But to be explicit and sure, we can call it here too?
        // No, DisposeAsync is reliable.
        // We just need a passing test that triggers the fixture/base teardown.
        Assert.True(true);
    }
}
