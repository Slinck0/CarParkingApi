using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace V2.Migrations
{
    /// <inheritdoc />
    public partial class InitAll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parking_lot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    Reserved = table.Column<int>(type: "INTEGER", nullable: false),
                    Tariff = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DayTariff = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreatedAt = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Lat = table.Column<double>(type: "REAL", nullable: false),
                    Lng = table.Column<double>(type: "REAL", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ClosedReason = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    ClosedDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_lot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reservation",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParkingLotId = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(10, 2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parking_lot_CreatedAt",
                table: "parking_lot",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_parking_lot_Location",
                table: "parking_lot",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_ParkingLotId",
                table: "reservation",
                column: "ParkingLotId");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_StartTime_EndTime",
                table: "reservation",
                columns: new[] { "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_UserId",
                table: "reservation",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_VehicleId",
                table: "reservation",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parking_lot");

            migrationBuilder.DropTable(
                name: "reservation");
        }
    }
}
