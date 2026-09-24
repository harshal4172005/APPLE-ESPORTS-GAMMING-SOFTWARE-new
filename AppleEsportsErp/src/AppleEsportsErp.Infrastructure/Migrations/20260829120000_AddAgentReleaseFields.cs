using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentReleaseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentFileName",
                table: "VersionInfos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentSha256",
                table: "VersionInfos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AgentSizeBytes",
                table: "VersionInfos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentFileName",
                table: "VersionInfos");

            migrationBuilder.DropColumn(
                name: "AgentSha256",
                table: "VersionInfos");

            migrationBuilder.DropColumn(
                name: "AgentSizeBytes",
                table: "VersionInfos");
        }
    }
}
