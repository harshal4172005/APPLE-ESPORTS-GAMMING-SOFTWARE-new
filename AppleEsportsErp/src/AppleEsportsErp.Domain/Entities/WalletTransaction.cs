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

    /// <summary>
    /// Who actually made this change, when it was a Head Office admin acting remotely - an id
    /// this branch's own `users` table (AdminId's FK target) has never heard of, since Head
    /// Office accounts are never synced down to a branch. Deliberately NOT foreign-keyed to
    /// anything local, so a remote correction can never fail with a FK violation the way
    /// writing it into AdminId did. AdminId itself is left null for a remote-issued row; a
    /// locally-made edit uses AdminId as before and leaves this null.
    /// </summary>
    public Guid? RemoteAdminId { get; set; }

    /// <summary>Display name to go with <see cref="RemoteAdminId"/>, captured at the moment of
    /// the edit so the audit trail still reads correctly even if that admin's name changes
    /// later.</summary>
    public string? RemoteAdminName { get; set; }

    public Guid? ShiftId { get; set; }
    public WalletAction Action { get; set; }
    public WalletType TargetWallet { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? PaymentType { get; set; } // cash, online, split
    public decimal CashAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public decimal BonusAmount { get; set; } // gaming top-up bonus, or an admin-granted bonus
    public Guid? BillId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Operator? Operator { get; set; }
    public User? Admin { get; set; }
    public Shift? Shift { get; set; }
    public Bill? Bill { get; set; }
}
