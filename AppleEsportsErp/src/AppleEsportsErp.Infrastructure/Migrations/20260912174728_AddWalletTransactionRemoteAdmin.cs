using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletTransactionRemoteAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RemoteAdminId",
                table: "wallet_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemoteAdminName",
                table: "wallet_transactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemoteAdminId",
                table: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "RemoteAdminName",
                table: "wallet_transactions");
        }
    }
}
