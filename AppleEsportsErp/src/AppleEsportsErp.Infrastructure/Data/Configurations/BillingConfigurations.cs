using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Data.Configurations;

/// <summary>SOP §9: Billing Counter — schema.sql L194-230</summary>
public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("bills");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.BillNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(e => e.BillNumber).IsUnique();
        builder.Property(e => e.CustomerName).HasMaxLength(100);

        // SOP: Gaming and food MUST remain separated — all DECIMAL(10,2)
        builder.Property(e => e.GamingAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.FoodAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.Subtotal).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.DiscountType).HasMaxLength(20)
            .HasConversion(v => v == null ? null : v.ToString()!.ToLowerInvariant(),
                           v => v == null ? null : Enum.Parse<DiscountType>(v, true));
        builder.Property(e => e.DiscountValue).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.DiscountAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.DiscountReason).HasColumnType("text");
        builder.Property(e => e.TotalAmount).HasPrecision(10, 2).HasDefaultValue(0m);

        // Payment
        builder.Property(e => e.PaymentType).HasMaxLength(20)
            .HasConversion(v => v == null ? null : v.ToString()!.ToLowerInvariant(),
                           v => v == null ? null : Enum.Parse<PaymentType>(v, true));
        builder.Property(e => e.CashAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.OnlineAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.WalletAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.CashReceived).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.ChangeReturned).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.ActualCashCollected).HasPrecision(10, 2).HasDefaultValue(0m);

        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(BillStatus.Pending)
            .HasConversion(v => v.ToString().ToLowerInvariant(),
                           v => Enum.Parse<BillStatus>(v, true));
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_bills_branch");
        builder.HasIndex(e => e.SessionId).HasDatabaseName("idx_bills_session");
        builder.HasIndex(e => e.OperatorId).HasDatabaseName("idx_bills_operator");
        builder.HasIndex(e => e.Status).HasDatabaseName("idx_bills_status");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_bills_date");

        builder.HasOne(e => e.Session).WithMany(s => s.Bills)
            .HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Pc).WithMany()
            .HasForeignKey(e => e.PcId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Branch).WithMany(b => b.Bills)
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Shift).WithMany(s => s.Bills)
            .HasForeignKey(e => e.ShiftId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Member).WithMany()
            .HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.DiscountByAdmin).WithMany()
            .HasForeignKey(e => e.DiscountBy).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Bill line items — schema.sql L241-251</summary>
public class BillItemConfiguration : IEntityTypeConfiguration<BillItem>
{
    public void Configure(EntityTypeBuilder<BillItem> builder)
    {
        builder.ToTable("bill_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.ItemType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Quantity).HasDefaultValue(1);
        builder.Property(e => e.UnitPrice).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.TotalPrice).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.BillId).HasDatabaseName("idx_bill_items_bill");

        builder.HasOne(e => e.Bill).WithMany(b => b.Items)
            .HasForeignKey(e => e.BillId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>SOP §9.3: Payment records — schema.sql L259-278</summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.PaymentType).HasMaxLength(20).IsRequired()
            .HasConversion(v => v.ToString().ToLowerInvariant(),
                           v => Enum.Parse<PaymentType>(v, true));
        builder.Property(e => e.TotalAmount).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.CashAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.OnlineAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.WalletAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.CashReceived).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.ChangeReturned).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.ActualCashCollected).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.GamingPortion).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.FoodPortion).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("completed");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.BillId).HasDatabaseName("idx_payments_bill");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_payments_branch");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_payments_date");
        builder.HasIndex(e => e.PaymentType).HasDatabaseName("idx_payments_type");

        builder.HasOne(e => e.Bill).WithMany(b => b.Payments)
            .HasForeignKey(e => e.BillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.Restrict);
    }
}
