using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingApi.Tests.Helpers;
using V2.Services;
using Xunit;

public class JwtExtensionsTests
{
    [Fact]
    public void AddJwtAuthentication_RegistersJwtAndAdminPolicy()
    {
        var services = new ServiceCollection();
        var config = TestConfigHelper.GetTestConfiguration();

        services.AddJwtAuthentication(config);
        var provider = services.BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme).Result;
        Assert.NotNull(scheme);

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
        var scheme = schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme).Result;
        Assert.NotNull(scheme);
    }
}


