using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AppleEsportsErp.Domain.Entities;

namespace AppleEsportsErp.Infrastructure.Data.Configurations;

public class OfflineSyncSessionConfiguration : IEntityTypeConfiguration<OfflineSyncSession>
{
    public void Configure(EntityTypeBuilder<OfflineSyncSession> builder)
    {
        builder.ToTable("offline_sync_sessions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnType("uuid").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.PcId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.DurationSeconds).IsRequired();
        builder.Property(e => e.OfflineStartTime).HasColumnType("timestamp with time zone");
        builder.Property(e => e.SessionType).HasMaxLength(50);
        builder.Property(e => e.SyncStatus).IsRequired().HasMaxLength(30).HasDefaultValue("pending");
        builder.Property(e => e.RawPayload).HasColumnType("jsonb");
        builder.Property(e => e.SyncedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");
        builder.HasIndex(e => e.PcId).HasDatabaseName("idx_offline_sync_sessions_pc");
        builder.HasIndex(e => e.SyncStatus).HasDatabaseName("idx_offline_sync_sessions_status");
        builder.HasIndex(e => e.SyncedAt).HasDatabaseName("idx_offline_sync_sessions_date");
    }
}

public class OfflineSyncBillingConfiguration : IEntityTypeConfiguration<OfflineSyncBilling>
{
    public void Configure(EntityTypeBuilder<OfflineSyncBilling> builder)
    {
        builder.ToTable("offline_sync_billings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnType("uuid").HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.BranchId).HasMaxLength(100);
        builder.Property(e => e.Amount).IsRequired().HasPrecision(10, 2);
        builder.Property(e => e.TransactionType).HasMaxLength(50);
        builder.Property(e => e.Timestamp).HasColumnType("timestamp with time zone");
        builder.Property(e => e.SyncStatus).IsRequired().HasMaxLength(30).HasDefaultValue("pending");
        builder.Property(e => e.RawPayload).HasColumnType("jsonb");
        builder.Property(e => e.SyncedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()");
        builder.HasIndex(e => e.SyncStatus).HasDatabaseName("idx_offline_sync_billings_status");
        builder.HasIndex(e => e.SyncedAt).HasDatabaseName("idx_offline_sync_billings_date");
    }
}
