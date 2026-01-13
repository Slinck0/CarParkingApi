using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Handlers;
using V2.Helpers;
using System.Threading.Tasks;
using System;

namespace ParkingApi.Tests.Handlers;

public class ParkingLotHandlerTests
{
    // --- CREATE TESTS ---

    [Fact]
    public async Task CreateParkingLot_ReturnsCreated_WhenDataIsValid()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();

        // Omdat het een record is, gebruiken we de constructor (volgorde is belangrijk!)
        var request = new ParkingLotCreate(
            "Nieuwe Garage",    // Name
            "Rotterdam",        // Location
            "Coolsingel 10",    // Address
            100,                // Capacity
            0,                  // Reserved
            5.0m,               // Tariff
            25.0m,              // DayTariff
            51.9225,            // Lat
            4.47917,            // Lng
            "Open",             // Status
            null,               // ClosedReason
            null                // ClosedDate
        );

        // Act
        var result = await ParkingLotHandlers.CreateParkingLot(request, db);

        // Assert
        Assert.DoesNotContain("BadRequest", result.ToString());
        
        // Check DB
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

        // Act
        var result = await ParkingLotHandlers.CreateParkingLot(request, db);

        // Assert
        Assert.IsType<BadRequest<string>>(result);
    }

    // --- UPDATE TESTS ---

    [Fact]
    public async Task UpdateParkingLot_ReturnsOk_WhenUpdateIsSuccessful()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        // Eerst een bestaande garage in de DB zetten
        var existingLot = new ParkingLotModel { 
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

        // Nieuwe data voor de update
        var updateRequest = new ParkingLotCreate(
            "Gewijzigde Naam",  // Nieuwe naam
            "Rotterdam", 
            "Weena 1", 
            200,                // Nieuwe capaciteit
            0,
            6.0m, 
            30.0m, 
            52.0, 
            4.0, 
            "Closed",           // Nieuwe status
            "Maintenance",      // Reden
            null
        );

        // Act
        var result = await ParkingLotHandlers.UpdateParkingLot(1, updateRequest, db);

        // Assert
        Assert.DoesNotContain("BadRequest", result.ToString());
        Assert.DoesNotContain("NotFound", result.ToString());

        // Check in DB
        var updatedLot = await db.ParkingLots.FindAsync(1);
        Assert.Equal("Gewijzigde Naam", updatedLot!.Name);
        Assert.Equal("Closed", updatedLot.Status);
    }

    [Fact]
    public async Task UpdateParkingLot_ReturnsNotFound_WhenIdDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        // Lege database, dus ID 99 bestaat niet

        var request = new ParkingLotCreate("Test", "L", "A", 100, 0, 1m, 1m, 1, 1, "Open", null, null);

        // Act
        var result = await ParkingLotHandlers.UpdateParkingLot(99, request, db);

        // Assert
        // We verwachten NotFound (geen OK)
        Assert.False(result.GetType().ToString().Contains("Ok"));
    }

    [Fact]
    public async Task UpdateParkingLot_ReturnsBadRequest_WhenDataIsInvalid()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        var existingLot = new ParkingLotModel { Id = 1, Name = "Oud", Capacity = 50, Location="L", Address="A", Tariff=1, DayTariff=1, Lat=1, Lng=1, CreatedAt = DateOnly.FromDateTime(DateTime.Now) };
        db.ParkingLots.Add(existingLot);
        db.SaveChanges();

        // FOUTE DATA: Lege naam
        var invalidRequest = new ParkingLotCreate("", "L", "A", 0, 0, 1m, 1m, 1, 1, "Open", null, null);

        // Act
        var result = await ParkingLotHandlers.UpdateParkingLot(1, invalidRequest, db);

        // Assert
        Assert.IsType<BadRequest<string>>(result);
    }

    // --- DELETE TESTS ---

    [Fact]
    public async Task DeleteParkingLot_ReturnsOk_AndRemovesItem()
    {
        // Arrange
        using var db = DbContextHelper.GetInMemoryDbContext();
        db.ParkingLots.Add(new ParkingLotModel { Id = 1, Name = "Te Verwijderen", Capacity = 10, Location="L", Address="A", Tariff=1, DayTariff=1, Lat=1, Lng=1, CreatedAt = DateOnly.FromDateTime(DateTime.Now) });
        db.SaveChanges();

        // Act
        var result = await ParkingLotHandlers.DeleteParkingLot(1, db);

        // Assert
        Assert.DoesNotContain("NotFound", result.ToString());
        Assert.Equal(0, await db.ParkingLots.CountAsync());
    }

    [Fact]
    public async Task DeleteParkingLot_ReturnsNotFound_WhenIdDoesNotExist()
    {
        using var db = DbContextHelper.GetInMemoryDbContext();
        
        // Act: ID 999 verwijderen uit lege DB
        var result = await ParkingLotHandlers.DeleteParkingLot(999, db);

        // Assert
        Assert.False(result.GetType().ToString().Contains("Ok"));
    }
}