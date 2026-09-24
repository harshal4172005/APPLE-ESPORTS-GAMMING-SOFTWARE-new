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
    public decimal CashReceived { get; set; }
    public decimal ChangeReturned { get; set; }
    public decimal ActualCashCollected { get; set; }
    public decimal GamingAmount { get; set; }
    public decimal FoodAmount { get; set; }
    public string TransactionType { get; set; } = null!;
    public string? CustomerName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>What the shift-start screen needs to know before it asks anything about cash.</summary>
public class RegisterOpeningDto
{
    /// <summary>
    /// True when there is nothing to compare a count against, so the operator enters what they
    /// physically see with nothing suggested. Two cases: this branch has genuinely never had a
    /// register before, or the last one closed on a "last shift of the day" tick - which usually
    /// means that day's cash left the drawer for the owner, so today's real count could be
    /// anything and carrying yesterday's figure forward would be actively wrong, not just
    /// unverified.
    /// </summary>
    public bool IsFirstOfDay { get; set; }

    /// <summary>
    /// What the last shift counted (or, failing that, what the system expected them to have).
    /// Shown to the operator as a reference figure to check the drawer against - not something
    /// silently opened with. See CashRegisterService.OpenRegisterAsync for how the operator's
    /// own count is compared against this.
    /// </summary>
    public decimal InheritedBalance { get; set; }

    /// <summary>True when a drawer is already open and this operator simply carries on with it.</summary>
    public bool AlreadyOpen { get; set; }
}

public class OpenRegisterDto
{
    [Required]
    [Range(0, 1000000)]
    public decimal OpeningBalance { get; set; }

    /// <summary>
    /// Only needed - and only read - when OpeningBalance disagrees with what
    /// RegisterOpeningDto.InheritedBalance said to expect. The first attempt is sent without
    /// one; if the counted figure does not match, OpenRegisterAsync refuses and hands back the
    /// difference instead of opening, and the operator's second attempt carries this.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// What opening the register actually did: opened it outright, or refused because the
/// operator's own count disagreed with what was expected and no reason has been given yet.
/// </summary>
public class OpenRegisterResultDto
{
    /// <summary>False only for the one case above - a genuine drawer error still throws, the
    /// same as it always has.</summary>
    public bool Opened { get; set; }

    /// <summary>Set when Opened is true.</summary>
    public CashRegisterDto? Register { get; set; }

    /// <summary>Set when Opened is false, so the operator can be shown what disagreed and asked why.</summary>
    public decimal? ExpectedBalance { get; set; }
    public decimal? CountedBalance { get; set; }
    public decimal? Difference { get; set; }
}

public class AddCashTransactionDto
{
    [Required]
    [Range(-1000000, 1000000)]
    public decimal Amount { get; set; }
    
    [Required]
    public string TransactionType { get; set; } = null!; // petty_expense, withdrawal, inward
    [Required(ErrorMessage = "Reason is mandatory for manual cash transactions.")]
    public string Reason { get; set; } = null!;
}
