using System;
using System.Collections.Generic;
using AppleEsportsErp.Application.DTOs.Cash;

namespace AppleEsportsErp.Application.DTOs.SystemDesks;

public class OnlineDeskSummaryDto
{
    public Guid ShiftId { get; set; }
    public decimal TotalOnlineSales { get; set; }
    public List<OnlineTransactionDto> Transactions { get; set; } = new();
}

public class OnlineTransactionDto
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
}

public class CashDeskSummaryDto
{
    public Guid ShiftId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal TotalCashSales { get; set; }
    public List<CashTransactionDto> Transactions { get; set; } = new();
}

public class WalletDeskSummaryDto
{
    public Guid ShiftId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal TotalWalletTopUps { get; set; }
    public decimal TotalWalletDeductions { get; set; }
    public List<WalletTransactionSummaryDto> Transactions { get; set; } = new();
}

public class WalletTransactionSummaryDto
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Action { get; set; } = string.Empty; // TopUp or Deduction

    /// <summary>Set only for a deduction that paid a gaming session's bill - which PC it was
    /// and how long it ran, so a deduction isn't just a bare number. Null for a top-up, or a
    /// deduction that isn't tied to a session (e.g. a food-only bill).</summary>
    public string? PcName { get; set; }
    public int? DurationMinutes { get; set; }
}
