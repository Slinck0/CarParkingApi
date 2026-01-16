using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace V2.Migrations
{
    /// <inheritdoc />
    public partial class parkin_sessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_Email",
                table: "user");

            migrationBuilder.AlterColumn<int>(
                name: "BirthYear",
                table: "user",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<decimal>(
                name: "DayTariff",
                table: "parking_lot",
                type: "decimal(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.CreateTable(
                name: "parking_sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_Email",
                table: "user",
                column: "Email");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "IX_user_Email",
                table: "user");

            migrationBuilder.AlterColumn<int>(
                name: "BirthYear",
                table: "user",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DayTariff",
                table: "parking_lot",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_Email",
                table: "user",
                column: "Email",
                unique: true);
        }
    }
}
