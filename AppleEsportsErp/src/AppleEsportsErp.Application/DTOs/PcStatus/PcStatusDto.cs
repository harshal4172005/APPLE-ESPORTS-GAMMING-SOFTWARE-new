using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Application.DTOs.PcStatus;

public class PcStatusDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public PcState State { get; set; }
    public Guid BranchId { get; set; }
    
    // Agent Connectivity
    public bool IsAgentOnline { get; set; }
    public string? ConnectionMode { get; set; }
    
    // Active session details (if busy or awaiting billing)
    public Guid? ActiveSessionId { get; set; }
    public Guid? ActiveBillId { get; set; }
    public DateTimeOffset? SessionStartTime { get; set; }
    public DateTimeOffset? SessionEndTime { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerType { get; set; }  // "Walk-in" | "Member"
    public decimal RatePerHour { get; set; }   // For live charge calculation on frontend
    public decimal TotalAmount { get; set; }   // Actual total accumulated charge for the session
    public string? Zone { get; set; }           // Standard / VIP / Console / Streaming
    public string? MonitorHz { get; set; }
    
    // For quickly restarting a recently completed session
    public string? LastCustomerName { get; set; }
    public Guid? LastMemberId { get; set; }

    // Upcoming reservation details (if reserved)
    public Guid? NextReservationId { get; set; }
    public DateTimeOffset? NextReservationTime { get; set; }

    public bool HasOverrunWarning { get; set; }
    public string? OverrunWarningMessage { get; set; }
}
