using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPackagePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PackagePrice",
                table: "sessions",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PricingPackageId",
                table: "sessions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackagePrice",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "PricingPackageId",
                table: "sessions");
        }
    }
}
