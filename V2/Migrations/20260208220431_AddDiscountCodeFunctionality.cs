using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace V2.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountCodeFunctionality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "reservation",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountCode",
                table: "reservation",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalCost",
                table: "reservation",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "discount",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "discount",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentUsageCount",
                table: "discount",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FixedAmount",
                table: "discount",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "discount",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "MaxReservationDuration",
                table: "discount",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsageCount",
                table: "discount",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "MinReservationDuration",
                table: "discount",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "discount",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkingLotId",
                table: "discount",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "discount",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "discount",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "discount_usage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReservationId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discount_usage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_DiscountCode",
                table: "reservation",
                column: "DiscountCode");

            migrationBuilder.CreateIndex(
                name: "IX_discount_IsActive",
                table: "discount",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_discount_OrganizationId",
                table: "discount",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_discount_ParkingLotId",
                table: "discount",
                column: "ParkingLotId");

            migrationBuilder.CreateIndex(
                name: "IX_discount_UserId",
                table: "discount",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_discount_ValidUntil",
                table: "discount",
                column: "ValidUntil");

            migrationBuilder.CreateIndex(
                name: "IX_discount_usage_DiscountId",
                table: "discount_usage",
                column: "DiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_discount_usage_ReservationId",
                table: "discount_usage",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_discount_usage_UsedAt",
                table: "discount_usage",
                column: "UsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_discount_usage_UserId",
                table: "discount_usage",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discount_usage");

            migrationBuilder.DropIndex(
                name: "IX_reservation_DiscountCode",
                table: "reservation");

            migrationBuilder.DropIndex(
                name: "IX_discount_IsActive",
                table: "discount");

            migrationBuilder.DropIndex(
                name: "IX_discount_OrganizationId",
                table: "discount");

            migrationBuilder.DropIndex(
                name: "IX_discount_ParkingLotId",
                table: "discount");

            migrationBuilder.DropIndex(
                name: "IX_discount_UserId",
                table: "discount");

            migrationBuilder.DropIndex(
                name: "IX_discount_ValidUntil",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "reservation");

            migrationBuilder.DropColumn(
                name: "DiscountCode",
                table: "reservation");

            migrationBuilder.DropColumn(
                name: "OriginalCost",
                table: "reservation");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "CurrentUsageCount",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "FixedAmount",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "MaxReservationDuration",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "MaxUsageCount",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "MinReservationDuration",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "ParkingLotId",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "discount");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "discount");
        }
    }
}
