using Microsoft.EntityFrameworkCore;
using V2.Data;
using System;

namespace ParkingApi.Tests.Helpers;

public static class DbContextHelper
{
    public static AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}