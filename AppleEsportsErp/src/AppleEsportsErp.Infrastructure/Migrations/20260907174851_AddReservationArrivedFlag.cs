using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationArrivedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Arrived",
                table: "reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Arrived",
                table: "reservations");
        }
    }
}
