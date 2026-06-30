using System;

namespace AppleEsportsErp.Application.DTOs.Eod;

public class EodSnapshotDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public DateTimeOffset ReportDate { get; set; }
    public Guid GeneratedByOperatorId { get; set; }
    
    public int SnapshotVersion { get; set; }
    public string SchemaVersion { get; set; } = null!;
    
    public EodReportDto Data { get; set; } = null!;
    
    public DateTimeOffset CreatedAt { get; set; }
}

public class ValidationStatusDto
{
    public bool IsReady { get; set; }
    public List<string> Blockers { get; set; } = new();
}
