using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace V2.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "vehicle",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "user",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganizationRole",
                table: "user",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkingLotId",
                table: "parking_sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "parking_sessions",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "parking_lot",
                type: "INTEGER",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_OrganizationId",
                table: "vehicle",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_user_OrganizationId",
                table: "user",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_parking_lot_OrganizationId",
                table: "parking_lot",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_discount_Code",
                table: "discount",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_parking_lot_organization_OrganizationId",
                table: "parking_lot",
                column: "OrganizationId",
                principalTable: "organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_organization_OrganizationId",
                table: "user",
                column: "OrganizationId",
                principalTable: "organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_vehicle_organization_OrganizationId",
                table: "vehicle",
                column: "OrganizationId",
                principalTable: "organization",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_parking_lot_organization_OrganizationId",
                table: "parking_lot");

            migrationBuilder.DropForeignKey(
                name: "FK_user_organization_OrganizationId",
                table: "user");

            migrationBuilder.DropForeignKey(
                name: "FK_vehicle_organization_OrganizationId",
                table: "vehicle");

            migrationBuilder.DropTable(
                name: "discount");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_OrganizationId",
                table: "vehicle");

            migrationBuilder.DropIndex(
                name: "IX_user_OrganizationId",
                table: "user");

            migrationBuilder.DropIndex(
                name: "IX_parking_lot_OrganizationId",
                table: "parking_lot");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "vehicle");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "user");

            migrationBuilder.DropColumn(
                name: "OrganizationRole",
                table: "user");

            migrationBuilder.DropColumn(
                name: "ParkingLotId",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "parking_lot");
        }
    }
}
