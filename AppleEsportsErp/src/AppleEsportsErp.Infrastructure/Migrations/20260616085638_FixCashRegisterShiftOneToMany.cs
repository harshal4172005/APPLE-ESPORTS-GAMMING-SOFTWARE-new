using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCashRegisterShiftOneToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_cash_register_shift;");
            migrationBuilder.Sql("ALTER TABLE wallet_transactions ADD COLUMN IF NOT EXISTS \"TargetWallet\" character varying(20) NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE reservations ADD COLUMN IF NOT EXISTS \"AdvanceDeposit\" numeric(18,2) NOT NULL DEFAULT 0.0;");
            migrationBuilder.Sql("ALTER TABLE inventory ADD COLUMN IF NOT EXISTS \"SoldQty\" integer NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS idx_cash_register_shift ON cash_register (\"ShiftId\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_cash_register_shift",
                table: "cash_register");

            migrationBuilder.DropColumn(
                name: "TargetWallet",
                table: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "AdvanceDeposit",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "SoldQty",
                table: "inventory");

            migrationBuilder.CreateIndex(
                name: "idx_cash_register_shift",
                table: "cash_register",
                column: "ShiftId",
                unique: true);
        }
    }
}
