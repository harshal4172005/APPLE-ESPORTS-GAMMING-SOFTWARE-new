using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §10: Shift accountability — operator shift records</summary>
public class Shift
{
    public Guid Id { get; set; }
    public Guid OperatorId { get; set; }
    public Guid BranchId { get; set; }
    public DateTimeOffset LoginTime { get; set; }
    public DateTimeOffset? LogoutTime { get; set; }
    public string? DeviceInfo { get; set; } // JSONB
    public ShiftStatus Status { get; set; } = ShiftStatus.Active;
    /// <summary>SOP §18: Shift Summary stored at closure — JSONB</summary>
    public string? Summary { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Operator Operator { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Bill> Bills { get; set; } = new List<Bill>();
    public ICollection<CashRegister> CashRegisters { get; set; } = new List<CashRegister>();
}
