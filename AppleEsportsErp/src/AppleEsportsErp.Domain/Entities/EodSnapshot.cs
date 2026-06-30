using System;

namespace AppleEsportsErp.Domain.Entities;

public class EodSnapshot
{
    public Guid Id { get; set; }
    
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public DateTimeOffset ReportDate { get; set; }
    
    public Guid GeneratedByOperatorId { get; set; }
    public Operator? GeneratedByOperator { get; set; }
    
    public int SnapshotVersion { get; set; }
    public string SchemaVersion { get; set; } = null!;
    
    // The entire JSON payload
    public string SnapshotData { get; set; } = null!;
    
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
