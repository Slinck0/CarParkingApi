using Microsoft.Extensions.Configuration;


namespace ParkingApi.Tests.Helpers
{
    public static class TestConfigHelper
    {
        public static IConfiguration GetTestConfiguration()
        {
            var settings = new Dictionary<string, string>
            {
                ["Jwt:Key"] = "THIS_IS_A_TEST_SECRET_KEY_123456789",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings!)
                .Build();
        }
    }
}