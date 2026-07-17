using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPcAgentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE pcs ADD COLUMN IF NOT EXISTS ""ConnectionMode"" text NOT NULL DEFAULT 'None';
                ALTER TABLE pcs ADD COLUMN IF NOT EXISTS ""IsAgentOnline"" boolean NOT NULL DEFAULT false;
                ALTER TABLE pcs ADD COLUMN IF NOT EXISTS ""LastAgentHeartbeat"" timestamp with time zone NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionMode",
                table: "pcs");

            migrationBuilder.DropColumn(
                name: "IsAgentOnline",
                table: "pcs");

            migrationBuilder.DropColumn(
                name: "LastAgentHeartbeat",
                table: "pcs");
        }
    }
}
