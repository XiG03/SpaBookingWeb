using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaBookingWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddtoWorkSchedule1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "WorkSchedules",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "WorkSchedules");
        }
    }
}
