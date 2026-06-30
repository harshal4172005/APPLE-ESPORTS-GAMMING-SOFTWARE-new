using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §16: Branch Configuration</summary>
public class Branch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public TimeOnly OpeningTime { get; set; } = new(10, 0);
    public TimeOnly ClosingTime { get; set; } = new(2, 0);
    public BranchStatus Status { get; set; } = BranchStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<Operator> Operators { get; set; } = new List<Operator>();
    public ICollection<Pc> Pcs { get; set; } = new List<Pc>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<FoodOrder> FoodOrders { get; set; } = new List<FoodOrder>();
}
