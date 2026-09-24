using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Application.DTOs.Sessions;

public class SessionDto
{
    public Guid Id { get; set; }
    public Guid PcId { get; set; }
    public string PcName { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Guid OperatorId { get; set; }
    public Guid ShiftId { get; set; }
    
    public string? CustomerName { get; set; }
    public Guid? MemberId { get; set; }
    
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    
    public decimal DurationMinutes { get; set; }
    public decimal ExpectedAmount { get; set; }
    public string PackageName { get; set; } = null!;
    
    public SessionState Status { get; set; }
    public Guid BillId { get; set; }

    public decimal? WalletDeductedAmount { get; set; }
    public decimal? WalletShortfallAmount { get; set; }
}

public class SessionStartDto
{
    public Guid PcId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? MemberId { get; set; }
    public decimal DurationMinutes { get; set; }
    public string PackageName { get; set; } = null!;
    public decimal ExpectedAmount { get; set; }
    public string? Notes { get; set; }
}

public class SessionExtendDto
{
    public decimal AdditionalMinutes { get; set; }

    /// <summary>
    /// No longer used to price anything - SessionService.ExtendSessionAsync computes the real
    /// charge itself, from the branch's own plans for AdditionalMinutes, and ignores whatever
    /// arrives here. Kept only so an older client sending it does not fail validation; a client
    /// billing a fixed-duration extension at a plain hourly rate (Rs 200 for a 4-hour plan
    /// priced at Rs 180) was exactly the bug this stopped trusting.
    /// </summary>
    public decimal AdditionalAmount { get; set; }

    public string PackageName { get; set; } = null!;
}

public class SessionTransferDto
{
    public Guid TargetPcId { get; set; }
}

public class StopSessionDto
{
    public bool DeferPayment { get; set; } = false;
}
