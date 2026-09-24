using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeOperatorLinkAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OperatorId",
                table: "employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_OperatorId",
                table: "employees",
                column: "OperatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_employees_operators_OperatorId",
                table: "employees",
                column: "OperatorId",
                principalTable: "operators",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employees_operators_OperatorId",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_OperatorId",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "OperatorId",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "employees");
        }
    }
}
