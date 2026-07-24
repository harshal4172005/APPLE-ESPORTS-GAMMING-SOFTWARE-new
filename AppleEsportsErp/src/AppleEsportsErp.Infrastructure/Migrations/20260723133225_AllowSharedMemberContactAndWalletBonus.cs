using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowSharedMemberContactAndWalletBonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_members_mobile",
                table: "members");

            migrationBuilder.AddColumn<decimal>(
                name: "BonusAmount",
                table: "wallet_transactions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "idx_members_mobile",
                table: "members",
                column: "MobileNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_members_mobile",
                table: "members");

            migrationBuilder.DropColumn(
                name: "BonusAmount",
                table: "wallet_transactions");

            migrationBuilder.CreateIndex(
                name: "idx_members_mobile",
                table: "members",
                column: "MobileNumber",
                unique: true);
        }
    }
}
