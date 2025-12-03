using Microsoft.EntityFrameworkCore;
using ParkingImporter.Data;
using ParkingImporter.Models;
using ParkingApi.Services;
using V2.Handlers;

namespace ParkingApi.Endpoints;

public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapGet("/Health", () => "Parking API is running...");

        // ----------------------------------------------------
        // Authentication Endpoints
        // ----------------------------------------------------
        app.MapPost("/register", UserHandlers.Register)
           .WithTags("Authentication");

        app.MapPost("/login", UserHandlers.Login)
           .WithTags("Authentication");

        // ----------------------------------------------------
        // Profile Endpoints (GET, PUT, DELETE /profile)
        // ----------------------------------------------------
        var profileGroup = app.MapGroup("/profile").RequireAuthorization().WithTags("Profile");

        profileGroup.MapGet("", ProfileHandlers.GetProfile);
        profileGroup.MapPut("", ProfileHandlers.UpdateProfile);
        profileGroup.MapDelete("", ProfileHandlers.DeleteProfile);

        // ----------------------------------------------------
        // Authorized Groups
        // ----------------------------------------------------
        var reservationGroup = app.MapGroup("/reservations").RequireAuthorization().WithTags("Reservations");
        var sessionGroup = app.MapGroup("/parkinglots/{id}/sessions").RequireAuthorization().WithTags("Sessions");
        
        // ----------------------------------------------------
        // Reservation Endpoints
        // ----------------------------------------------------
        reservationGroup.MapPost("", ReservationHandlers.CreateReservation);
        reservationGroup.MapGet("/me", ReservationHandlers.GetMyReservations);
        reservationGroup.MapDelete("/{id}", ReservationHandlers.CancelReservation);
        reservationGroup.MapPut("/{id}", ReservationHandlers.UpdateReservation);

        // ----------------------------------------------------
        // Vehicle Endpoints
        // ----------------------------------------------------
        app.MapPost("/vehicles", VehicleHandlers.CreateVehicle)
           .RequireAuthorization().WithTags("Vehicles").WithName("CreateVehicle");
        
        app.MapGet("/vehicles", VehicleHandlers.GetMyVehicles)
           .RequireAuthorization().WithTags("Vehicles");

        app.MapPut("/vehicles/{id}", VehicleHandlers.UpdateVehicle)
           .RequireAuthorization().WithTags("Vehicles");
           
        app.MapDelete("vehicles/{id}", VehicleHandlers.DeleteVehicle)
           .RequireAuthorization().WithTags("Vehicles");
           
        // ----------------------------------------------------
        // Session Endpoints
        // ----------------------------------------------------
        sessionGroup.MapPost("/start", SessionHandlers.StartSession);
        sessionGroup.MapPost("/stop", SessionHandlers.StopSession);

        // ----------------------------------------------------
        // Billing Endpoints
        // ----------------------------------------------------
        var billingGroup = app.MapGroup("/billing").RequireAuthorization().WithTags("Billing");
        
        billingGroup.MapGet("", BillingHandlers.GetUpcomingPayments)
           .WithName("GetUpcomingPayments")
           .WithDescription("Get upcoming payments for the authenticated user");
           
        billingGroup.MapGet("/history", BillingHandlers.GetBillingHistory)
           .WithName("GetBillingHistory")
           .WithDescription("Get billing history for the authenticated user");

        // ----------------------------------------------------
        // Parking Lot Endpoints
        // ----------------------------------------------------
        app.MapPost("/parking-lots", ParkingLotHandlers.CreateParkingLot)
           .RequireAuthorization("ADMIN").WithTags("ParkingLots");
    }
}