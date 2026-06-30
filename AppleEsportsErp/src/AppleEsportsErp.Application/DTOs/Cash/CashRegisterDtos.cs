using AppleEsportsErp.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppleEsportsErp.Application.DTOs.Cash;

public class CashRegisterDto
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OperatorId { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalSplitCash { get; set; }
    public decimal ExpectedDrawerCash { get; set; }
    public decimal? PhysicalCashCounted { get; set; }
    public decimal? CashDifference { get; set; }
    public string? MismatchReason { get; set; }
    public CashRegisterStatus Status { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    
    public List<CashTransactionDto> Transactions { get; set; } = new();
}

public class CashTransactionDto
{
    public Guid Id { get; set; }
    public Guid? BillId { get; set; }
    public string? PcNumber { get; set; }
    public decimal CashAmount { get; set; }
    public decimal GamingAmount { get; set; }
    public decimal FoodAmount { get; set; }
    public string TransactionType { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class OpenRegisterDto
{
    [Required]
    [Range(0, 1000000)]
    public decimal OpeningBalance { get; set; }
}

public class AddCashTransactionDto
{
    [Required]
    [Range(-1000000, 1000000)]
    public decimal Amount { get; set; }
    
    [Required]
    public string TransactionType { get; set; } = null!; // petty_expense, withdrawal, inward
    
    public string? Reason { get; set; }
}
