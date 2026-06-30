namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §22: Immutable Audit Trail — every critical action logged, READ ONLY after creation</summary>
public class AuditLog
{
    public Guid Id { get; set; }
    // Who
    public Guid? UserId { get; set; }
    public Guid? OperatorId { get; set; }
    public string UserRole { get; set; } = null!;
    public string UserName { get; set; } = null!;
    // What
    public string Action { get; set; } = null!;
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    // Where
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    // Details
    public string? Details { get; set; } // JSONB
    public string? IpAddress { get; set; }
    public string? DeviceInfo { get; set; } // JSONB
    // When — SOP: exact date + timestamp
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Branch? Branch { get; set; }
}
