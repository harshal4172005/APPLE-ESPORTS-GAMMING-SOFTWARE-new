namespace AppleEsportsErp.Domain.Entities;

/// <summary>
/// Records offline cash transactions synced back from an Operator's PC after connectivity is restored.
/// Decentralized LAN Offline Architecture — SOP §9 offline resilience.
/// </summary>
public class OfflineSyncBilling
{
    public Guid Id { get; set; }

    /// <summary>Raw BranchId string submitted by the offline device.</summary>
    public string? BranchId { get; set; }

    /// <summary>Resolved FK to the Branches table after reconciliation. Null if unresolved.</summary>
    public Guid? ResolvedBranchId { get; set; }

    public decimal Amount { get; set; }

    public string? TransactionType { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>"pending" | "reconciled" | "conflict"</summary>
    public string SyncStatus { get; set; } = "pending";

    public string? Notes { get; set; }

    /// <summary>Full raw JSON payload stored for audit trail.</summary>
    public string? RawPayload { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public Guid? SubmittedByOperatorId { get; set; }
}
