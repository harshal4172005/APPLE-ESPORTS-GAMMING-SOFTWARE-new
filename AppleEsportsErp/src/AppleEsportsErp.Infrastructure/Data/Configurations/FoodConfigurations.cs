using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Data.Configurations;

/// <summary>SOP §13: Menu/Food Inventory — schema.sql L367-379</summary>
public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(100);
        builder.Property(e => e.Price).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.CurrentStock).HasDefaultValue(0);
        builder.Property(e => e.SoldQty).HasDefaultValue(0);
        builder.Property(e => e.MinStockLimit).HasDefaultValue(5);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(FoodAvailability.Available)
            .HasConversion(v => v.ToString().ToLowerInvariant().Replace("outofstock", "out_of_stock"),
                           v => Enum.Parse<FoodAvailability>(v.Replace("out_of_stock", "OutOfStock"), true));
        builder.Property(e => e.ImageUrl).HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_inventory_branch");
        builder.HasIndex(e => e.Status).HasDatabaseName("idx_inventory_status");
        builder.HasIndex(e => e.Category).HasDatabaseName("idx_inventory_category");

        builder.HasOne(e => e.Branch).WithMany(b => b.InventoryItems)
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Inventory logs — schema.sql L388-399</summary>
public class InventoryLogConfiguration : IEntityTypeConfiguration<InventoryLog>
{
    public void Configure(EntityTypeBuilder<InventoryLog> builder)
    {
        builder.ToTable("inventory_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.Action).HasMaxLength(30).IsRequired();
        builder.Property(e => e.OldValue).HasColumnType("text");
        builder.Property(e => e.NewValue).HasColumnType("text");
        builder.Property(e => e.Reason).HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.InventoryId).HasDatabaseName("idx_inv_logs_item");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_inv_logs_date");

        builder.HasOne(e => e.InventoryItem).WithMany(i => i.Logs)
            .HasForeignKey(e => e.InventoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>SOP §12: Food Orders — schema.sql L407-428</summary>
public class FoodOrderConfiguration : IEntityTypeConfiguration<FoodOrder>
{
    public void Configure(EntityTypeBuilder<FoodOrder> builder)
    {
        builder.ToTable("food_orders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.OrderNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(e => e.OrderNumber).IsUnique();
        builder.Property(e => e.CustomerName).HasMaxLength(100);
        builder.Property(e => e.TotalAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.PaymentType).HasMaxLength(20);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue(OrderStatus.Pending)
            .HasConversion(v => v.ToString().ToLowerInvariant(),
                           v => Enum.Parse<OrderStatus>(v, true));
        builder.Property(e => e.CancelledReason).HasColumnType("text");
        builder.Property(e => e.OrderTime).HasDefaultValueSql("NOW()");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.BranchId).HasDatabaseName("idx_food_orders_branch");
        builder.HasIndex(e => e.SessionId).HasDatabaseName("idx_food_orders_session");
        builder.HasIndex(e => e.Status).HasDatabaseName("idx_food_orders_status");
        builder.HasIndex(e => e.OrderTime).HasDatabaseName("idx_food_orders_date");

        builder.HasOne(e => e.Session).WithMany(s => s.FoodOrders)
            .HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Pc).WithMany()
            .HasForeignKey(e => e.PcId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Branch).WithMany(b => b.FoodOrders)
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operator).WithMany()
            .HasForeignKey(e => e.OperatorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Member).WithMany()
            .HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Food order items — schema.sql L438-447</summary>
public class FoodOrderItemConfiguration : IEntityTypeConfiguration<FoodOrderItem>
{
    public void Configure(EntityTypeBuilder<FoodOrderItem> builder)
    {
        builder.ToTable("food_order_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
        builder.Property(e => e.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Quantity).HasDefaultValue(1).IsRequired();
        builder.Property(e => e.UnitPrice).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.TotalPrice).HasPrecision(10, 2).IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(e => e.OrderId).HasDatabaseName("idx_food_items_order");

        builder.HasOne(e => e.FoodOrder).WithMany(fo => fo.Items)
            .HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.InventoryItem).WithMany()
            .HasForeignKey(e => e.InventoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
