namespace AppleEsportsErp.Domain.Entities;

/// <summary>
/// Records offline session telemetry synced back from a Gaming PC after connectivity is restored.
/// Decentralized LAN Offline Architecture — SOP §7 offline resilience.
/// </summary>
public class OfflineSyncSession
{
    public Guid Id { get; set; }

    /// <summary>Raw PcId string submitted by the offline device (may be a Guid or a PcNumber string).</summary>
    public string PcId { get; set; } = default!;

    /// <summary>Resolved FK to the Pcs table after reconciliation. Null if unresolved.</summary>
    public Guid? ResolvedPcId { get; set; }

    public Guid? BranchId { get; set; }

    public int DurationSeconds { get; set; }

    public DateTimeOffset OfflineStartTime { get; set; }

    /// <summary>e.g. "Cash", "Member"</summary>
    public string? SessionType { get; set; }

    /// <summary>"pending" | "reconciled" | "conflict"</summary>
    public string SyncStatus { get; set; } = "pending";

    public string? Notes { get; set; }

    /// <summary>Full raw JSON payload stored for audit trail.</summary>
    public string? RawPayload { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public Guid? SubmittedByOperatorId { get; set; }
}
