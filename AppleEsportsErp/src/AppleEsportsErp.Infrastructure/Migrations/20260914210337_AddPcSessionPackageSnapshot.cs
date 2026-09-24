using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPcSessionPackageSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentSessionPackagePrice",
                table: "pcs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentSessionPlannedDurationMin",
                table: "pcs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentSessionPackagePrice",
                table: "pcs");

            migrationBuilder.DropColumn(
                name: "CurrentSessionPlannedDurationMin",
                table: "pcs");
        }
    }
}
