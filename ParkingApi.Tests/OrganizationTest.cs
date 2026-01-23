using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Reflection;
using V2.Helpers;
using V2.Models;

namespace ParkingApi.Tests.Handlers;

public class OrganizationHandlersTests
{
    private static object? GetValue(object result)
        => result.GetType().GetProperty("Value")?.GetValue(result);

    // ---------- helpers ----------
    private static Type FindType(string simpleName)
    {
        var asm = typeof(OrganizationModel).Assembly;
        var t = asm.GetTypes().FirstOrDefault(x => x.Name == simpleName);
        if (t is null) throw new InvalidOperationException($"Type '{simpleName}' not found in V2 assembly.");
        return t;
    }

    private static async Task<IResult> InvokeStaticAsync(Type type, string methodName, params object?[] args)
    {
        var mi = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .FirstOrDefault(m => m.Name == methodName);

        if (mi is null) throw new InvalidOperationException($"Method '{type.Name}.{methodName}' not found.");

        var raw = mi.Invoke(null, args);

        if (raw is IResult r) return r;

        if (raw is Task t)
        {
            await t.ConfigureAwait(false);
            var tt = t.GetType();
            if (tt.IsGenericType)
            {
                var res = tt.GetProperty("Result")!.GetValue(t);
                return (IResult)res!;
            }
        }

        throw new InvalidOperationException($"Unexpected return type from '{type.Name}.{methodName}'.");
    }

    private static object CreateRoleRequest(MethodInfo mi, string role)
    {
        var p = mi.GetParameters()
                  .First(x => x.ParameterType != typeof(int) &&
                              x.ParameterType.Name != "AppDbContext");

        var reqType = p.ParameterType;

        var ctor = reqType.GetConstructors()
                          .FirstOrDefault(c =>
                          {
                              var ps = c.GetParameters();
                              return ps.Length == 1 && ps[0].ParameterType == typeof(string);
                          });

        if (ctor != null)
            return ctor.Invoke(new object?[] { role });

        var inst = Activator.CreateInstance(reqType)
                   ?? throw new InvalidOperationException($"Cannot create instance of {reqType.Name}");

        var prop = reqType.GetProperty("OrganizationRole") ?? reqType.GetProperty("Role");
        if (prop is null || !prop.CanWrite)
            throw new InvalidOperationException($"Cannot set role on {reqType.Name} (no writable OrganizationRole/Role).");

        prop.SetValue(inst, role);
        return inst;
    }

    private static void AssertStatus(IResult result, params int[] allowed)
    {
        var sc = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Contains(sc.StatusCode ?? 0, allowed);
    }

    private static object? GetValue(IResult result)
        => result.GetType().GetProperty("Value")?.GetValue(result);

    private static IEnumerable ExtractList(object? value, params string[] propNames)
    {
        if (value is null) return Array.Empty<object>();

        if (value is IEnumerable e && value is not string)
            return e;

        foreach (var name in propNames)
        {
            var prop = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop?.GetValue(value) is IEnumerable pe && pe is not string)
                return pe;
        }

        return Array.Empty<object>();
    }

    // ---------- tests ----------

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

    [Fact]
    public async Task AdminAssignUserToOrganization_UpdatesUserAndVehicles()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });

        db.Users.Add(new UserModel
        {
            Id = 10,
            Username = "u10",
            Password = "pw",
            Name = "User 10",
            Email = "u10@test.com",
            Phone = "06",
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });

        db.Vehicles.Add(new VehicleModel
        {
            Id = 1,
            UserId = 10,
            LicensePlate = "AA-10-AA",
            Make = "M",
            Model = "X",
            Color = "C",
            Year = 2020,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });

        db.Vehicles.Add(new VehicleModel
        {
            Id = 2,
            UserId = 10,
            LicensePlate = "BB-10-BB",
            Make = "M",
            Model = "Y",
            Color = "C",
            Year = 2021,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });

        await db.SaveChangesAsync();

        var userHandlers = FindType("UserHandlers");
        var mi = userHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .First(m => m.Name == "AdminAssignUserToOrganization");

        var req = CreateRoleRequest(mi, "EMPLOYEE");

        var result = await InvokeStaticAsync(userHandlers, "AdminAssignUserToOrganization", 1, 10, req, db);

        AssertStatus(result, StatusCodes.Status200OK);

        var user = await db.Users.FirstAsync(u => u.Id == 10);

        var orgIdProp = user.GetType().GetProperty("OrganizationId");
        var orgRoleProp = user.GetType().GetProperty("OrganizationRole");

        Assert.NotNull(orgIdProp);
        Assert.NotNull(orgRoleProp);

        Assert.Equal(1, orgIdProp!.GetValue(user));
        Assert.Equal("EMPLOYEE", ((string?)orgRoleProp!.GetValue(user))?.ToUpperInvariant());

        var vehicles = await db.Vehicles.Where(v => v.UserId == 10).ToListAsync();
        var vOrgProp = vehicles[0].GetType().GetProperty("OrganizationId");
        Assert.NotNull(vOrgProp);

        Assert.All(vehicles, v => Assert.Equal(1, vOrgProp!.GetValue(v)));
    }

    [Fact]
    public async Task AdminAssignUserToOrganization_ReturnsBadRequest_OnInvalidRole()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });
        db.Users.Add(new UserModel
        {
            Id = 10,
            Username = "u10",
            Password = "pw",
            Name = "User 10",
            Email = "u10@test.com",
            Phone = "06",
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });
        await db.SaveChangesAsync();

        var userHandlers = FindType("UserHandlers");
        var mi = userHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .First(m => m.Name == "AdminAssignUserToOrganization");

        var req = CreateRoleRequest(mi, "NOT_A_ROLE");

        var result = await InvokeStaticAsync(userHandlers, "AdminAssignUserToOrganization", 1, 10, req, db);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task AdminUpdateUserOrganizationRole_UpdatesRole()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });

        var user = new UserModel
        {
            Id = 10,
            Username = "u10",
            Password = "pw",
            Name = "User 10",
            Email = "u10@test.com",
            Phone = "06",
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        };

        // set org fields via reflection (so compilation doesn’t depend on exact model shape)
        user.GetType().GetProperty("OrganizationId")?.SetValue(user, 1);
        user.GetType().GetProperty("OrganizationRole")?.SetValue(user, "EMPLOYEE");

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var userHandlers = FindType("UserHandlers");
        var mi = userHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .First(m => m.Name == "AdminUpdateUserOrganizationRole");

        var req = CreateRoleRequest(mi, "ADMIN");

        var result = await InvokeStaticAsync(userHandlers, "AdminUpdateUserOrganizationRole", 1, 10, req, db);

        AssertStatus(result, StatusCodes.Status200OK);

        var updated = await db.Users.FirstAsync(u => u.Id == 10);
        var orgRoleProp = updated.GetType().GetProperty("OrganizationRole");
        Assert.Equal("ADMIN", ((string?)orgRoleProp?.GetValue(updated))?.ToUpperInvariant());
    }

    [Fact]
    public async Task AdminRemoveUserFromOrganization_ClearsUserAndVehicleOrg()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });

        var user = new UserModel
        {
            Id = 10,
            Username = "u10",
            Password = "pw",
            Name = "User 10",
            Email = "u10@test.com",
            Phone = "06",
            Active = true,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        };

        user.GetType().GetProperty("OrganizationId")?.SetValue(user, 1);
        user.GetType().GetProperty("OrganizationRole")?.SetValue(user, "EMPLOYEE");

        db.Users.Add(user);

        var vehicle = new VehicleModel
        {
            Id = 1,
            UserId = 10,
            LicensePlate = "AA-10-AA",
            Make = "M",
            Model = "X",
            Color = "C",
            Year = 2020,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        };
        vehicle.GetType().GetProperty("OrganizationId")?.SetValue(vehicle, 1);

        db.Vehicles.Add(vehicle);

        await db.SaveChangesAsync();

        var asm = typeof(OrganizationModel).Assembly;
        var userHandlers = asm.GetTypes().First(t => t.Name == "UserHandlers");

        var mi = userHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .First(m => m.Name == "AdminRemoveUserFromOrganization");

        var args = new object?[mi.GetParameters().Length];
        for (int i = 0; i < mi.GetParameters().Length; i++)
        {
            var p = mi.GetParameters()[i];

            if (p.ParameterType.Name == "AppDbContext")
                args[i] = db;
            else if (p.ParameterType == typeof(int))
            {
                var n = (p.Name ?? "").ToLowerInvariant();
                if (n.Contains("org")) args[i] = 1;
                else if (n.Contains("user")) args[i] = 10;
                else args[i] = 0;
            }
            else
                args[i] = null;
        }

        var raw = mi.Invoke(null, args);

        IResult result;
        if (raw is IResult r)
        {
            result = r;
        }
        else
        {
            var t = (Task)raw!;
            await t.ConfigureAwait(false);
            result = (IResult)t.GetType().GetProperty("Result")!.GetValue(t)!;
        }

        var sc = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Contains(sc.StatusCode ?? 0, new[] { StatusCodes.Status204NoContent, StatusCodes.Status200OK });

        var updatedUser = await db.Users.FirstAsync(u => u.Id == 10);
        Assert.Null(updatedUser.GetType().GetProperty("OrganizationId")?.GetValue(updatedUser));
        Assert.Null(updatedUser.GetType().GetProperty("OrganizationRole")?.GetValue(updatedUser));

        var vehicles = await db.Vehicles
            .IgnoreQueryFilters()
            .Where(v => v.UserId == 10)
            .ToListAsync();

        Assert.Single(vehicles);
        Assert.Null(vehicles[0].GetType().GetProperty("OrganizationId")?.GetValue(vehicles[0]));
    }


    [Fact]
    public async Task AdminGetOrganizationUsers_ReturnsOnlyOrgUsers()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });

        var u1 = new UserModel { Id = 1, Username = "u1", Password = "pw", Name = "u1", Email = "u1@t.com", Phone = "06", Active = true };
        var u2 = new UserModel { Id = 2, Username = "u2", Password = "pw", Name = "u2", Email = "u2@t.com", Phone = "06", Active = true };
        u1.GetType().GetProperty("OrganizationId")?.SetValue(u1, 1);
        u2.GetType().GetProperty("OrganizationId")?.SetValue(u2, null);

        db.Users.AddRange(u1, u2);
        await db.SaveChangesAsync();

        var userHandlers = FindType("UserHandlers");
        var result = await InvokeStaticAsync(userHandlers, "AdminGetOrganizationUsers", 1, db);

        AssertStatus(result, StatusCodes.Status200OK);

        var value = GetValue(result);
        var users = ExtractList(value, "users", "Users");
        Assert.Single(users.Cast<object>());
    }

    [Fact]
    public async Task AdminCreateParkingLotForOrganization_CreatesWithOrgId()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });
        await db.SaveChangesAsync();

        var req = new ParkingLotCreate(
            "Org Lot",
            "Amsterdam",
            "Street 1",
            100,
            0,
            5m,
            25m,
            52.0,
            4.0,
            "Open",
            null,
            null
        );

        var parkingLotHandlers = FindType("ParkingLotHandlers");
        var result = await InvokeStaticAsync(parkingLotHandlers, "AdminCreateParkingLotForOrganization", 1, req, db);

        AssertStatus(result, StatusCodes.Status201Created);

        var saved = await db.ParkingLots.FirstAsync();
        Assert.Equal(1, saved.OrganizationId);
        Assert.Equal("Org Lot", saved.Name);
    }

    [Fact]
    public async Task AdminGetOrganizationParkingLots_ReturnsOnlyOrgLots()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });

        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 1,
            OrganizationId = 1,
            Name = "Lot1",
            Capacity = 10,
            Location = "L",
            Address = "A",
            Tariff = 1,
            DayTariff = 1,
            Lat = 1,
            Lng = 1,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });

        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 2,
            OrganizationId = null,
            Name = "Lot2",
            Capacity = 10,
            Location = "L",
            Address = "A",
            Tariff = 1,
            DayTariff = 1,
            Lat = 1,
            Lng = 1,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });

        await db.SaveChangesAsync();

        var parkingLotHandlers = FindType("ParkingLotHandlers");
        var result = await InvokeStaticAsync(parkingLotHandlers, "AdminGetOrganizationParkingLots", 1, db);

        AssertStatus(result, StatusCodes.Status200OK);

        var value = GetValue(result);
        var lots = ExtractList(value, "parkingLots", "ParkingLots");
        Assert.Single(lots.Cast<object>());
    }

    [Fact]
    public async Task AdminGetOrganizationVehicles_ReturnsOnlyOrgVehicles()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });
        await db.SaveChangesAsync();

        var v1 = new VehicleModel { Id = 1, UserId = 10, LicensePlate = "AA-01-AA", Make = "M", Model = "X", Color = "C", Year = 2020, CreatedAt = DateOnly.FromDateTime(DateTime.Now) };
        var v2 = new VehicleModel { Id = 2, UserId = 11, LicensePlate = "BB-02-BB", Make = "M", Model = "Y", Color = "C", Year = 2021, CreatedAt = DateOnly.FromDateTime(DateTime.Now) };

        v1.GetType().GetProperty("OrganizationId")?.SetValue(v1, 1);
        v2.GetType().GetProperty("OrganizationId")?.SetValue(v2, null);

        db.Vehicles.AddRange(v1, v2);
        await db.SaveChangesAsync();

        var vehicleHandlers = FindType("VehicleHandlers");
        var result = await InvokeStaticAsync(vehicleHandlers, "AdminGetOrganizationVehicles", 1, db);

        AssertStatus(result, StatusCodes.Status200OK);

        var value = GetValue(result);
        var vehicles = ExtractList(value, "vehicles", "Vehicles");
        Assert.Single(vehicles.Cast<object>());
    }
}
