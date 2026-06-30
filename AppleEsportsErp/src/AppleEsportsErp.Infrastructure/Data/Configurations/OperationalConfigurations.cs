using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Data.Configurations;

/// <summary>SOP §10: Shift accountability — schema.sql L106-117</summary>
public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("shifts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.LoginTime).IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(e => e.DeviceInfo).HasColumnType("jsonb");
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(ShiftStatus.Active)
            .HasConversion(v => v.ToString().ToLowerInvariant().Replace("forceclosed", "force_closed"),
                           v => Enum.Parse<ShiftStatus>(v.Replace("force_closed", "ForceClosed"), true));
        builder.Property(e => e.Summary).HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.OperatorId).HasDatabaseName("idx_shifts_operator");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_shifts_branch");
        builder.HasIndex(e => e.Status).HasDatabaseName("idx_shifts_status");

        builder.HasOne(e => e.Operator).WithMany(o => o.Shifts)
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany(b => b.Shifts)
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>SOP §7: Session Engine — schema.sql L127-150</summary>
public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.CustomerName).HasMaxLength(100);
        builder.Property(e => e.StartTime).IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(e => e.GamingAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.FoodAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TotalAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.State).HasMaxLength(20).HasDefaultValue(SessionState.Active)
            .HasConversion(v => v.ToString().ToLowerInvariant().Replace("awaitingbilling", "awaiting_billing"),
                           v => Enum.Parse<SessionState>(v.Replace("awaiting_billing", "AwaitingBilling"), true));
        builder.Property(e => e.GamingType).HasMaxLength(50).HasDefaultValue("standard");
        builder.Property(e => e.Notes).HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.PcId).HasDatabaseName("idx_sessions_pc");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_sessions_branch");
        builder.HasIndex(e => e.State).HasDatabaseName("idx_sessions_state");
        builder.HasIndex(e => e.OperatorId).HasDatabaseName("idx_sessions_operator");
        builder.HasIndex(e => e.StartTime).HasDatabaseName("idx_sessions_date");

        builder.HasOne(e => e.Pc).WithMany(p => p.Sessions)
            .HasForeignKey(e => e.PcId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany(b => b.Sessions)
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany(o => o.Sessions)
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Shift).WithMany(s => s.Sessions)
            .HasForeignKey(e => e.ShiftId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Member).WithMany()
            .HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>SOP §8: Reservation System — schema.sql L162-183</summary>
public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.CustomerName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ReservationTime).IsRequired();
        builder.Property(e => e.GracePeriodMin).HasDefaultValue(15);
        builder.Property(e => e.AdvanceDeposit).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(e => e.State).HasMaxLength(20).HasDefaultValue(ReservationState.Pending)
            .HasConversion(v => v.ToString().ToLowerInvariant(),
                           v => Enum.Parse<ReservationState>(v, true));
        builder.Property(e => e.OverrideReason).HasColumnType("text");
        builder.Property(e => e.Notes).HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.PcId).HasDatabaseName("idx_reservations_pc");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_reservations_branch");
        builder.HasIndex(e => e.State).HasDatabaseName("idx_reservations_state");
        builder.HasIndex(e => e.ReservationTime).HasDatabaseName("idx_reservations_time");

        builder.HasOne(e => e.Pc).WithMany(p => p.Reservations)
            .HasForeignKey(e => e.PcId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany(b => b.Reservations)
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Member).WithMany()
            .HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.OverrideByAdmin).WithMany()
            .HasForeignKey(e => e.OverrideBy).OnDelete(DeleteBehavior.SetNull);
    }
}
