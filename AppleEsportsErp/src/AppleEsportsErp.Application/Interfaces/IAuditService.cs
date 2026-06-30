namespace AppleEsportsErp.Application.Interfaces;

/// <summary>
/// Audit service contract — maps from audit.js
/// SOP §22: Immutable Audit Trail — every critical action MUST be logged
/// </summary>
public interface IAuditService
{
    /// <summary>Log an audit event — INSERT only, no UPDATE/DELETE</summary>
    Task LogAsync(AuditEntry entry);
}

/// <summary>Structured audit entry matching the audit_logs table schema</summary>
public class AuditEntry
{
    public Guid? UserId { get; set; }
    public Guid? OperatorId { get; set; }
    public string UserRole { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public object? Details { get; set; }
    public string? IpAddress { get; set; }
    public object? DeviceInfo { get; set; }
}
