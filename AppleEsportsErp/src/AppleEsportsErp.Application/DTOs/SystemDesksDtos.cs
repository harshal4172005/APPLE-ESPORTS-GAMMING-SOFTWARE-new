using System;
using System.Collections.Generic;

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

public class WalletDeskSummaryDto
{
    public Guid ShiftId { get; set; }
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
}
