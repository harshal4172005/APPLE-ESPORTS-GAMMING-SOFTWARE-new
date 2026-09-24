using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodGroupsAndSharedStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceRelayEventId",
                table: "inventory_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FoodGroupId",
                table: "branches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "food_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_inv_logs_relay_event",
                table: "inventory_logs",
                column: "SourceRelayEventId",
                unique: true,
                filter: "\"SourceRelayEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_branches_FoodGroupId",
                table: "branches",
                column: "FoodGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_branches_food_groups_FoodGroupId",
                table: "branches",
                column: "FoodGroupId",
                principalTable: "food_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_branches_food_groups_FoodGroupId",
                table: "branches");

            migrationBuilder.DropTable(
                name: "food_groups");

            migrationBuilder.DropIndex(
                name: "idx_inv_logs_relay_event",
                table: "inventory_logs");

            migrationBuilder.DropIndex(
                name: "IX_branches_FoodGroupId",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "SourceRelayEventId",
                table: "inventory_logs");

            migrationBuilder.DropColumn(
                name: "FoodGroupId",
                table: "branches");
        }
    }
}
