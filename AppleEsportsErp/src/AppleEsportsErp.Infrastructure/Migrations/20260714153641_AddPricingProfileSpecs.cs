using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingProfileSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefreshRate",
                table: "PricingProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemSpecs",
                table: "PricingProfiles",
                type: "text",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefreshRate",
                table: "PricingProfiles");

            migrationBuilder.DropColumn(
                name: "SystemSpecs",
                table: "PricingProfiles");
        }
    }
}
