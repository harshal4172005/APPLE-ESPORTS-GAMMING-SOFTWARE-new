using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §8: Reservation System — centralized state reflected everywhere</summary>
public class Reservation
{
    public Guid Id { get; set; }
    public Guid PcId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OperatorId { get; set; }
    public string CustomerName { get; set; } = null!;
    public Guid? MemberId { get; set; }
    public DateTimeOffset ReservationTime { get; set; }
    public int? DurationMin { get; set; }
    public int GracePeriodMin { get; set; } = 15;
    public decimal AdvanceDeposit { get; set; }

    // State tracking
    public ReservationState State { get; set; } = ReservationState.Pending;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    // Override tracking — SOP: requires permission + reason
    public Guid? OverrideBy { get; set; }
    public string? OverrideReason { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Pc Pc { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Operator Operator { get; set; } = null!;
    public Member? Member { get; set; }
    public User? OverrideByAdmin { get; set; }
}
