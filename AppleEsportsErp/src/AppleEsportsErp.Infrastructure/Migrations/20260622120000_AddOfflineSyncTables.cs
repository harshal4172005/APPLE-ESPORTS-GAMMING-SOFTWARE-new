using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleEsportsErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineSyncTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offline_sync_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    PcId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResolvedPcId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    OfflineStartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SessionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SyncStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "pending"),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    SubmittedByOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_sync_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "offline_sync_billings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BranchId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResolvedBranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "pending"),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    SubmittedByOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_sync_billings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_offline_sync_sessions_pc",
                table: "offline_sync_sessions",
                column: "PcId");

            migrationBuilder.CreateIndex(
                name: "idx_offline_sync_sessions_status",
                table: "offline_sync_sessions",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "idx_offline_sync_sessions_date",
                table: "offline_sync_sessions",
                column: "SyncedAt");

            migrationBuilder.CreateIndex(
                name: "idx_offline_sync_billings_status",
                table: "offline_sync_billings",
                column: "SyncStatus");

            migrationBuilder.CreateIndex(
                name: "idx_offline_sync_billings_date",
                table: "offline_sync_billings",
                column: "SyncedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "offline_sync_sessions");
            migrationBuilder.DropTable(name: "offline_sync_billings");
        }
    }
}
