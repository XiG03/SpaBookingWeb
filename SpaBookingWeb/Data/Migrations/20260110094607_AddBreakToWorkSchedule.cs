using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaBookingWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBreakToWorkSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "BreakStartTime",
                table: "WorkSchedules",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnBreak",
                table: "WorkSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreakStartTime",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "IsOnBreak",
                table: "WorkSchedules");
        }
    }
}
