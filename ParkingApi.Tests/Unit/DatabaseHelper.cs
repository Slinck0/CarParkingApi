using Microsoft.EntityFrameworkCore;
using V2.Data;

namespace ParkingApi.Tests.Unit;

public static class DbContextHelper
{
    public static AppDbContext GetInMemoryDbContext()
    {
        // Use in-memory database with unique name per test for isolation
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
