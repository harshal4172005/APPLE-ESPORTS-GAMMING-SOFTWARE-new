using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §7.1: PC stations per branch with state tracking</summary>
public class Pc
{
    public Guid Id { get; set; }
    public string PcNumber { get; set; } = null!;
    public Guid BranchId { get; set; }
    public PcState State { get; set; } = PcState.Idle;
    public Guid? CurrentSessionId { get; set; }
    public Guid? CurrentReservationId { get; set; }
    public DateTimeOffset? LastActiveAt { get; set; }
    public Guid? LastOperatorId { get; set; }
    public string? IpAddress { get; set; }
    public string? Specs { get; set; } // JSONB
    public string? PcName { get; set; }
    public string? Zone { get; set; }
    public Guid? PricingProfileId { get; set; }
    public string? HardwareNotes { get; set; }
    public string? MonitorHz { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    
    // Agent connection tracking
    public bool IsAgentOnline { get; set; } = false;
    public string ConnectionMode { get; set; } = "None";  // "LAN", "Cloud", "None"
    public DateTimeOffset? LastAgentHeartbeat { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Branch Branch { get; set; } = null!;
    public Session? CurrentSession { get; set; }
    public Reservation? CurrentReservation { get; set; }
    public Operator? LastOperator { get; set; }
    public PricingProfile? PricingProfile { get; set; }
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
