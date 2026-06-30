using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §7: Session Engine — full gaming session lifecycle</summary>
public class Session
{
    public Guid Id { get; set; }
    public Guid PcId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OperatorId { get; set; }
    public Guid? ShiftId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? MemberId { get; set; }

    // Session timing
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public int? PlannedDurationMin { get; set; }
    public int? ActualDurationMin { get; set; }

    // Billing — SOP: gaming and food MUST remain separated
    public decimal GamingAmount { get; set; }
    public decimal FoodAmount { get; set; }
    public decimal TotalAmount { get; set; }

    // State
    public SessionState State { get; set; } = SessionState.Active;
    public string GamingType { get; set; } = "standard";
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Pc Pc { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Operator Operator { get; set; } = null!;
    public Shift? Shift { get; set; }
    public Member? Member { get; set; }
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
    public ICollection<FoodOrder> FoodOrders { get; set; } = new List<FoodOrder>();
}
