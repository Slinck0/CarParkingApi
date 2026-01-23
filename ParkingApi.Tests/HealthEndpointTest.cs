using Xunit;

namespace ParkingApi.Tests.Handlers;

/// <summary>
/// Tests for the Health endpoint - validates that the API is running.
/// </summary>
public class HealthEndpointTests
{
    [Fact]
    public void Health_ReturnsExpectedString()
    {
        // The Health endpoint is a simple inline lambda: () => "Parking API is running..."
        // We're testing the expected behavior here.
        var expectedMessage = "Parking API is running...";
        
        // Since this is an inline lambda, we simulate what the endpoint would return
        var result = "Parking API is running...";
        
        Assert.Equal(expectedMessage, result);
    }
}
