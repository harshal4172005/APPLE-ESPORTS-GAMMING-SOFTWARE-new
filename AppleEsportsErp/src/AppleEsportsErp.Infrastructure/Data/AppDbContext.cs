using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Domain.Entities;

namespace AppleEsportsErp.Infrastructure.Data;

/// <summary>
/// Gaming Café ERP — EF Core DbContext
/// Maps all 23 tables from schema.sql with full relational integrity.
/// SOP §23: Database Architecture
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── 23 DbSets matching schema.sql ──
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<PricingProfile> PricingProfiles => Set<PricingProfile>();
    public DbSet<Pc> Pcs => Set<Pc>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CustomerCredit> CustomerCredits => Set<CustomerCredit>();
    public DbSet<CashRegister> CashRegisters => Set<CashRegister>();
    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();
    public DbSet<DenominationCount> DenominationCounts => Set<DenominationCount>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryLog> InventoryLogs => Set<InventoryLog>();
    public DbSet<FoodOrder> FoodOrders => Set<FoodOrder>();
    public DbSet<FoodOrderItem> FoodOrderItems => Set<FoodOrderItem>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<LoyaltyPoint> LoyaltyPoints => Set<LoyaltyPoint>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<EodSnapshot> EodSnapshots => Set<EodSnapshot>();

    // Decentralized LAN Offline Architecture sync tables
    public DbSet<OfflineSyncSession> OfflineSyncSessions => Set<OfflineSyncSession>();
    public DbSet<OfflineSyncBilling> OfflineSyncBillings => Set<OfflineSyncBilling>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PostgreSQL extensions
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pgcrypto");

        // Hardening C.1: Optimistic Concurrency via PostgreSQL xmin
        modelBuilder.Entity<Session>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<Reservation>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<Bill>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<BillItem>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<Payment>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<CashRegister>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<CashTransaction>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<WalletTransaction>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<FoodOrder>().UseXminAsConcurrencyToken();
        modelBuilder.Entity<InventoryItem>().UseXminAsConcurrencyToken();

        // Apply all IEntityTypeConfiguration<T> from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
