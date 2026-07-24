using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Data.Configurations;

/// <summary>SOP §14: Members — schema.sql L454-477</summary>
public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.MemberNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(e => e.MemberNumber).IsUnique();
        builder.Property(e => e.FullName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MobileNumber).HasMaxLength(20).IsRequired();
        // Not unique: household members can share the same phone number as long as their name differs
        // (uniqueness of name+phone+email is enforced in MemberService, not at the DB level).
        builder.HasIndex(e => e.MobileNumber).HasDatabaseName("idx_members_mobile");
        builder.Property(e => e.Email).HasMaxLength(255);
        builder.Property(e => e.Username).HasMaxLength(50);
        builder.HasIndex(e => e.Username).IsUnique().HasFilter("\"Username\" IS NOT NULL").HasDatabaseName("IX_members_Username");
        builder.Property(e => e.PasswordHash).HasMaxLength(255);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(MemberStatus.Active)
            .HasConversion(v => v.ToString().ToLowerInvariant(),
                           v => Enum.Parse<MemberStatus>(v, true));
        builder.Property(e => e.GamingBalance).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.FoodBalance).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TotalGamingTopUps).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TotalGamingBonusEarned).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TotalGamingSpend).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TotalFoodSpend).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.JoinDate).HasDefaultValueSql("NOW()");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.Status).HasDatabaseName("idx_members_status");

        builder.HasOne(e => e.HomeBranch).WithMany()
            .HasForeignKey(e => e.HomeBranchId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>SOP §14.1: Wallet transactions — schema.sql L486-504</summary>
public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.TargetWallet).HasMaxLength(20).HasConversion<string>();
        builder.Property(e => e.Action).HasMaxLength(30).IsRequired()
            .HasConversion(v => v.ToString().ToLowerInvariant()
                .Replace("deductiongaming", "deduction_gaming")
                .Replace("deductionfood", "deduction_food")
                .Replace("rewardredemption", "reward_redemption"),
                v => Enum.Parse<WalletAction>(v
                .Replace("deduction_gaming", "DeductionGaming")
                .Replace("deduction_food", "DeductionFood")
                .Replace("reward_redemption", "RewardRedemption"), true));
        builder.Property(e => e.Amount).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.BalanceBefore).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.BalanceAfter).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.PaymentType).HasMaxLength(20);
        builder.Property(e => e.CashAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.OnlineAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.BonusAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.Reason).HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.MemberId).HasDatabaseName("idx_wallet_txn_member");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_wallet_txn_date");

        builder.HasOne(e => e.Member).WithMany(m => m.WalletTransactions)
            .HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Admin).WithMany()
            .HasForeignKey(e => e.AdminId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Bill).WithMany()
            .HasForeignKey(e => e.BillId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>SOP §15: Loyalty points — schema.sql L512-528</summary>
public class LoyaltyPointConfiguration : IEntityTypeConfiguration<LoyaltyPoint>
{
    public void Configure(EntityTypeBuilder<LoyaltyPoint> builder)
    {
        builder.ToTable("loyalty_points");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.Action).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Points).IsRequired();
        builder.Property(e => e.PointsBefore).IsRequired();
        builder.Property(e => e.PointsAfter).IsRequired();
        builder.Property(e => e.RewardType).HasMaxLength(50);
        builder.Property(e => e.RewardValue).HasPrecision(10, 2);
        builder.Property(e => e.Reason).HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.MemberId).HasDatabaseName("idx_loyalty_member");

        builder.HasOne(e => e.Member).WithMany(m => m.LoyaltyPoints)
            .HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Admin).WithMany()
            .HasForeignKey(e => e.AdminId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Bill).WithMany()
            .HasForeignKey(e => e.BillId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>SOP §9.6: Discounts — schema.sql L535-545</summary>
public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("discounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.DiscountType).HasMaxLength(20).IsRequired()
            .HasConversion(v => v.ToString().ToLowerInvariant(),
                           v => Enum.Parse<DiscountType>(v, true));
        builder.Property(e => e.DiscountValue).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.DiscountAmount).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.Reason).HasColumnType("text").IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.Bill).WithMany()
            .HasForeignKey(e => e.BillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Admin).WithMany()
            .HasForeignKey(e => e.AdminId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>SOP §22: Audit Logs — schema.sql L551-571, READ ONLY</summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.UserRole).HasMaxLength(20).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(50).IsRequired();
        builder.Property(e => e.TargetType).HasMaxLength(50);
        builder.Property(e => e.BranchName).HasMaxLength(100);
        builder.Property(e => e.Details).HasColumnType("jsonb");
        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.DeviceInfo).HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.UserId).HasDatabaseName("idx_audit_user");
        builder.HasIndex(e => e.OperatorId).HasDatabaseName("idx_audit_operator");
        builder.HasIndex(e => e.Action).HasDatabaseName("idx_audit_action");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_audit_branch");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_audit_date");
        builder.HasIndex(e => new { e.TargetType, e.TargetId }).HasDatabaseName("idx_audit_target");

        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>SOP §17: System Config — schema.sql L585-592</summary>
public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("system_config");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.ConfigKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.ConfigKey).IsUnique();
        builder.Property(e => e.ConfigValue).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Description).HasColumnType("text");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.UpdatedByAdmin).WithMany()
            .HasForeignKey(e => e.UpdatedBy).OnDelete(DeleteBehavior.SetNull);
    }
}
