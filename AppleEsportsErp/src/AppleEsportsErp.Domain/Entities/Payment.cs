using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §9.3: Payment records with cash/online/split/wallet tracking</summary>
public class Payment
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OperatorId { get; set; }
    public PaymentType PaymentType { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public decimal WalletAmount { get; set; }

    // SOP §15A: Cash tracking
    public decimal CashReceived { get; set; }
    public decimal ChangeReturned { get; set; }
    public decimal ActualCashCollected { get; set; }

    // Gaming/Food split
    public decimal GamingPortion { get; set; }
    public decimal FoodPortion { get; set; }
    public string Status { get; set; } = "completed";
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Bill Bill { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Operator Operator { get; set; } = null!;
}
