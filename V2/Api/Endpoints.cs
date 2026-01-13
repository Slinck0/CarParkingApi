using Microsoft.EntityFrameworkCore;
using V2.Data;
using V2.Models;
using V2.Services;

namespace V2.Endpoints;

public static class Endpoints
{
   public static void MapEndpoints(this WebApplication app)
   {
      app.MapGet("/Health", () => "Parking API is running...");


      app.MapPost("/register", UserHandlers.Register)
         .WithTags("Authentication");

      app.MapPost("/login", UserHandlers.Login)
         .WithTags("Authentication");


      var profileGroup = app.MapGroup("/profile").RequireAuthorization().WithTags("Profile");

        profileGroup.MapGet("", ProfileHandlers.GetProfile);
        profileGroup.MapPut("", ProfileHandlers.UpdateProfile);
        profileGroup.MapDelete("", ProfileHandlers.DeleteProfile);
        


      var reservationGroup = app.MapGroup("/reservations").RequireAuthorization().WithTags("Reservations");
      var sessionGroup = app.MapGroup("/parkinglots/{id}/sessions").RequireAuthorization().WithTags("Sessions");


      reservationGroup.MapPost("", ReservationHandlers.CreateReservation);
      reservationGroup.MapGet("/me", ReservationHandlers.GetMyReservations);
      reservationGroup.MapDelete("/{id}", ReservationHandlers.CancelReservation);
      reservationGroup.MapPut("/{id}", ReservationHandlers.UpdateReservation);


      app.MapPost("/vehicles", VehicleHandlers.CreateVehicle)
         .RequireAuthorization().WithTags("Vehicles").WithName("CreateVehicle");

      app.MapGet("/vehicles", VehicleHandlers.GetMyVehicles)
         .RequireAuthorization().WithTags("Vehicles");

      app.MapPut("/vehicles/{id}", VehicleHandlers.UpdateVehicle)
         .RequireAuthorization().WithTags("Vehicles");

      app.MapDelete("vehicles/{id}", VehicleHandlers.DeleteVehicle)
         .RequireAuthorization().WithTags("Vehicles");


      sessionGroup.MapPost("/start", SessionHandlers.StartSession);
      sessionGroup.MapPost("/stop", SessionHandlers.StopSession);

        // ----------------------------------------------------
        // Parking Lot Endpoints
        // ----------------------------------------------------
        app.MapPost("/parking-lots", ParkingLotHandlers.CreateParkingLot)
           .RequireAuthorization("ADMIN").WithTags("ParkingLots");

        // ----------------------------------------------------
        // Admin Endpoints
        // ----------------------------------------------------
        var adminGroup = app.MapGroup("/admin")
           .RequireAuthorization("ADMIN")
           .WithTags("Admin");

        adminGroup.MapPut("/users/{id}/toggle-active", ProfileHandlers.UpdateState);
    }
}