using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §14.1: Wallet System — every action stores payment type, operator, date/time, amount</summary>
public class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? OperatorId { get; set; }
    public Guid? AdminId { get; set; }
    public WalletAction Action { get; set; }
    public WalletType TargetWallet { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? PaymentType { get; set; } // cash, online, split
    public decimal CashAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public Guid? BillId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Operator? Operator { get; set; }
    public User? Admin { get; set; }
    public Bill? Bill { get; set; }
}
