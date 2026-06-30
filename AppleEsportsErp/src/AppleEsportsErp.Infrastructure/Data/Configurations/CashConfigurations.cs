using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Data.Configurations;

/// <summary>SOP §10: Cash Register — schema.sql L289-309</summary>
public class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> builder)
    {
        builder.ToTable("cash_register");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.OpeningBalance).HasPrecision(10, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(e => e.TotalCashSales).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TotalSplitCash).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.ExpectedDrawerCash).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.PhysicalCashCounted).HasPrecision(10, 2);
        builder.Property(e => e.CashDifference).HasPrecision(10, 2);
        builder.Property(e => e.MismatchReason).HasColumnType("text");
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(CashRegisterStatus.Open)
            .HasConversion(v => v.ToString().ToLowerInvariant(),
                           v => Enum.Parse<CashRegisterStatus>(v, true));
        builder.Property(e => e.OpenedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.ShiftId).HasDatabaseName("idx_cash_register_shift");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_cash_register_branch");

        builder.HasOne(e => e.Shift).WithMany(s => s.CashRegisters)
            .HasForeignKey(e => e.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cash transactions — schema.sql L317-330</summary>
public class CashTransactionConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public void Configure(EntityTypeBuilder<CashTransaction> builder)
    {
        builder.ToTable("cash_transactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.PcNumber).HasMaxLength(20);
        builder.Property(e => e.CashAmount).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.GamingAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.FoodAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TransactionType).HasMaxLength(30).HasDefaultValue("billing");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.CashRegisterId).HasDatabaseName("idx_cash_txn_register");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_cash_txn_date");

        builder.HasOne(e => e.CashRegister).WithMany(cr => cr.CashTransactions)
            .HasForeignKey(e => e.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Bill).WithMany()
            .HasForeignKey(e => e.BillId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>SOP §11.1: Denomination Counter — schema.sql L339-362</summary>
public class DenominationCountConfiguration : IEntityTypeConfiguration<DenominationCount>
{
    public void Configure(EntityTypeBuilder<DenominationCount> builder)
    {
        builder.ToTable("denomination_counts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.CountedTotal).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.ExpectedTotal).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.Difference).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.IsVerified).HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.CashRegister).WithMany(cr => cr.DenominationCounts)
            .HasForeignKey(e => e.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Shift).WithMany()
            .HasForeignKey(e => e.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}
