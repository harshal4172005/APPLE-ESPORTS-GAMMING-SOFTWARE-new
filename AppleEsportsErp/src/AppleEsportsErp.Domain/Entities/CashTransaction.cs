namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §9: Individual cash transaction log</summary>
public class CashTransaction
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public Guid? BillId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OperatorId { get; set; }
    public string? PcNumber { get; set; }
    public decimal CashAmount { get; set; }
    public decimal GamingAmount { get; set; }
    public decimal FoodAmount { get; set; }
    public string TransactionType { get; set; } = "billing"; // billing, wallet_recharge, refund
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public CashRegister CashRegister { get; set; } = null!;
    public Bill? Bill { get; set; }
    public Branch Branch { get; set; } = null!;
    public Operator Operator { get; set; } = null!;
}
