using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ParkingApi.Tests.Helpers;
using Xunit;

public class JwtExtensionsTests
{
    [Fact]
    public void AddJwtAuthentication_RegistersJwtAndAdminPolicy()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = TestConfigHelper.GetTestConfiguration();

        // Act
        services.AddJwtAuthentication(config);
        var provider = services.BuildServiceProvider();

        // Assert JWT authentication
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = schemeProvider
            .GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme)
            .Result;

        Assert.NotNull(scheme);

        // Assert ADMIN policy
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var adminPolicy = policyProvider.GetPolicyAsync("ADMIN").Result;

        Assert.NotNull(adminPolicy);
    }

    [Fact]
    public void AddJwtAuthentication_UsesFallbackValues_WhenConfigIsMissing()
    {
        var services = new ServiceCollection();
        var emptyConfig = new ConfigurationBuilder().Build();

        services.AddJwtAuthentication(emptyConfig);
        var provider = services.BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = schemeProvider
            .GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme)
            .Result;

        Assert.NotNull(scheme);
    }
}