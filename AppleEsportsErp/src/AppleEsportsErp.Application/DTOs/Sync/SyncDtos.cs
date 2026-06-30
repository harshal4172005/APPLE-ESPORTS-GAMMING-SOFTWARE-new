using System.ComponentModel.DataAnnotations;

namespace AppleEsportsErp.Application.DTOs.Sync;

/// <summary>Offline session telemetry submitted by a Gaming PC after connectivity is restored.</summary>
public class SyncOfflineSessionDto
{
    [Required]
    [MaxLength(100)]
    public string PcId { get; set; } = default!;

    [Range(1, int.MaxValue)]
    public int DurationSeconds { get; set; }

    public DateTimeOffset OfflineStartTime { get; set; }

    [MaxLength(50)]
    public string? SessionType { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Offline cash transaction submitted by an Operator's PC after connectivity is restored.</summary>
public class SyncOfflineBillingDto
{
    [MaxLength(100)]
    public string? BranchId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string? TransactionType { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Result of an offline sync operation.</summary>
public class SyncResultDto
{
    public bool Success { get; set; }
    public Guid SyncId { get; set; }
    public string Status { get; set; } = default!;
    public string Message { get; set; } = default!;
}
