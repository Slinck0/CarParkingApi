using Xunit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using V2.Models;
using V2.Helpers;
using System;
using System.Threading.Tasks;

namespace ParkingApi.Tests.Unit.Handlers;

public class ParkingLotHandlerTests
{
    [Fact]
    public async Task CreateParkingLot_ReturnsCreated_WhenDataIsValid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new ParkingLotCreate(
            "Nieuwe Garage",
            "Rotterdam",
            "Coolsingel 10",
            100,
            0,
            5.0m,
            25.0m,
            51.9225,
            4.47917,
            "Open",
            null,
            null
        );

        var result = await ParkingLotHandlers.CreateParkingLot(request, db);

        Assert.DoesNotContain("BadRequest", result.ToString());
        Assert.Equal(1, await db.ParkingLots.CountAsync());

        var savedLot = await db.ParkingLots.FirstAsync();
        Assert.Equal("Nieuwe Garage", savedLot.Name);
    }

    [Fact]
    public async Task CreateParkingLot_ReturnsBadRequest_WhenDataIsInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new ParkingLotCreate(
            "",
            "Rotterdam",
            "Coolsingel 10",
            -10,
            0,
            5.0m,
            25.0m,
            51.9225,
            4.47917,
            "Open",
            null,
            null
        );

        var result = await ParkingLotHandlers.CreateParkingLot(request, db);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task UpdateParkingLot_ReturnsOk_WhenUpdateIsSuccessful()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var existingLot = new ParkingLotModel
        {
            Id = 1,
            Name = "Oude Naam",
            Capacity = 50,
            Location = "X",
            Address = "Y",
            Tariff = 1,
            DayTariff = 10,
            Lat = 1,
            Lng = 1,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        };
        db.ParkingLots.Add(existingLot);
        db.SaveChanges();

        var updateRequest = new ParkingLotCreate(
            "Gewijzigde Naam",
            "Rotterdam",
            "Weena 1",
            200,
            0,
            6.0m,
            30.0m,
            52.0,
            4.0,
            "Closed",
            "Maintenance",
            null
        );

        var result = await ParkingLotHandlers.UpdateParkingLot(1, updateRequest, db);

        Assert.DoesNotContain("BadRequest", result.ToString());
        Assert.DoesNotContain("NotFound", result.ToString());

        var updatedLot = await db.ParkingLots.FindAsync(1);
        Assert.Equal("Gewijzigde Naam", updatedLot!.Name);
        Assert.Equal("Closed", updatedLot.Status);
    }

    [Fact]
    public async Task UpdateParkingLot_ReturnsNotFound_WhenIdDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var request = new ParkingLotCreate("Test", "L", "A", 100, 0, 1m, 1m, 1, 1, "Open", null, null);
        var result = await ParkingLotHandlers.UpdateParkingLot(99, request, db);

        Assert.False(result.GetType().ToString().Contains("Ok"));
    }

    [Fact]
    public async Task UpdateParkingLot_ReturnsBadRequest_WhenDataIsInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        var existingLot = new ParkingLotModel
        {
            Id = 1,
            Name = "Oud",
            Capacity = 50,
            Location = "L",
            Address = "A",
            Tariff = 1,
            DayTariff = 1,
            Lat = 1,
            Lng = 1,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        };
        db.ParkingLots.Add(existingLot);
        db.SaveChanges();

        var invalidRequest = new ParkingLotCreate("", "L", "A", 0, 0, 1m, 1m, 1, 1, "Open", null, null);
        var result = await ParkingLotHandlers.UpdateParkingLot(1, invalidRequest, db);

        Assert.IsType<BadRequest<string>>(result);
    }

    [Fact]
    public async Task AdminDeleteParkingLotFromOrganization_ReturnsNoContent_AndRemovesItem()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });
        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 1,
            OrganizationId = 1,
            Name = "Te Verwijderen",
            Capacity = 10,
            Location = "L",
            Address = "A",
            Tariff = 1,
            DayTariff = 1,
            Lat = 1,
            Lng = 1,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });
        db.SaveChanges();

        var result = await ParkingLotHandlers.AdminDeleteParkingLotFromOrganization(1, 1, db);

        Assert.IsType<NoContent>(result);
        Assert.Equal(0, await db.ParkingLots.CountAsync());
    }

    [Fact]
    public async Task AdminDeleteParkingLotFromOrganization_ReturnsConflict_WhenWrongOrg()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });
        db.Organizations.Add(new OrganizationModel { Id = 2, Name = "Org 2" });

        db.ParkingLots.Add(new ParkingLotModel
        {
            Id = 1,
            OrganizationId = 1,
            Name = "Lot",
            Capacity = 10,
            Location = "L",
            Address = "A",
            Tariff = 1,
            DayTariff = 1,
            Lat = 1,
            Lng = 1,
            CreatedAt = DateOnly.FromDateTime(DateTime.Now)
        });
        db.SaveChanges();

        var result = await ParkingLotHandlers.AdminDeleteParkingLotFromOrganization(2, 1, db);

        Assert.IsType<Conflict<string>>(result);
    }

    [Fact]
    public async Task AdminDeleteParkingLotFromOrganization_ReturnsNotFound_WhenIdDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();

        db.Organizations.Add(new OrganizationModel { Id = 1, Name = "Org 1" });
        db.SaveChanges();

        var result = await ParkingLotHandlers.AdminDeleteParkingLotFromOrganization(1, 999, db);

        Assert.IsType<NotFound<string>>(result);
    }
}

