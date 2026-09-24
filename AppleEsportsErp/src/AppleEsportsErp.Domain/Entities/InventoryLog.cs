namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §12: Stock refill/reduction history</summary>
public class InventoryLog
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? OperatorId { get; set; }
    public string Action { get; set; } = null!; // refill, sale, wastage, price_change, status_change
    public int? Quantity { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set only when this row was written by applying a shared-stock relay from a
    /// sibling branch (see BranchHeartbeatService.RunRelaySharedStockDeltaAsync). Unique when
    /// not null — lets a redelivered relay instruction be recognised and skipped instead of
    /// double-applying the same stock movement.</summary>
    public Guid? SourceRelayEventId { get; set; }

    // Navigation
    public InventoryItem InventoryItem { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Operator? Operator { get; set; }
}
