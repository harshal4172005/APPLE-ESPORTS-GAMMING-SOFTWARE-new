using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPcAgentVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentVersion",
                table: "pcs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentVersion",
                table: "pcs");
        }
    }
}
