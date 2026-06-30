namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §11.1: Denomination Counter — operator enters at shift end</summary>
public class DenominationCount
{
    public Guid Id { get; set; }
    public Guid CashRegisterId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OperatorId { get; set; }

    // Indian Rupee denomination breakdown
    public int Notes2000 { get; set; }
    public int Notes500 { get; set; }
    public int Notes200 { get; set; }
    public int Notes100 { get; set; }
    public int Notes50 { get; set; }
    public int Notes20 { get; set; }
    public int Notes10 { get; set; }
    public int Coins5 { get; set; }
    public int Coins2 { get; set; }
    public int Coins1 { get; set; }

    public decimal CountedTotal { get; set; }
    public decimal ExpectedTotal { get; set; }
    public decimal Difference { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public CashRegister CashRegister { get; set; } = null!;
    public Shift Shift { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Operator Operator { get; set; } = null!;
}
