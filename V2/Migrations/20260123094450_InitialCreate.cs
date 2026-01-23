using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace V2.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discount",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discount", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parking_sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    LicensePlate = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ParkingLotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment",
                columns: table => new
                {
                    Transaction = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Initiator = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Bank = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ReservationId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment", x => x.Transaction);
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

            migrationBuilder.CreateTable(
                name: "parking_lot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    Reserved = table.Column<int>(type: "INTEGER", nullable: false),
                    Tariff = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DayTariff = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_parking_lot_organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrganizationRole = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    BirthYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "vehicle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<int>(type: "INTEGER", nullable: true),
                    LicensePlate = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Make = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_discount_Code",
                table: "discount",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_CreatedAt",
                table: "organization",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_organization_Name",
                table: "organization",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parking_lot_CreatedAt",
                table: "parking_lot",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_parking_lot_Location",
                table: "parking_lot",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_parking_lot_OrganizationId",
                table: "parking_lot",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_EndTime",
                table: "parking_sessions",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_StartTime",
                table: "parking_sessions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_UserId",
                table: "parking_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_parking_sessions_VehicleId",
                table: "parking_sessions",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_ReservationId",
                table: "payment",
                column: "ReservationId");

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

            migrationBuilder.CreateIndex(
                name: "IX_user_Email",
                table: "user",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_user_OrganizationId",
                table: "user",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_CreatedAt",
                table: "vehicle",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_LicensePlate",
                table: "vehicle",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_OrganizationId",
                table: "vehicle",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_UserId",
                table: "vehicle",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discount");

            migrationBuilder.DropTable(
                name: "parking_lot");

            migrationBuilder.DropTable(
                name: "parking_sessions");

            migrationBuilder.DropTable(
                name: "payment");

            migrationBuilder.DropTable(
                name: "reservation");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "vehicle");

            migrationBuilder.DropTable(
                name: "organization");
        }
    }
}
