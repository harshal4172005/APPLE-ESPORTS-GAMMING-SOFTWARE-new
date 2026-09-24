using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AppleEsportsErp.Domain.Entities;

namespace AppleEsportsErp.Infrastructure.Data;

/// <summary>
/// Watches every save for a change to InventoryItem.CurrentStock, however it happened — a
/// food sale, a restock, a shift-start recount, code nobody has written yet — and records
/// exactly how much it moved by, not what it is now worth.
///
/// That distinction is the whole point. Two branches sharing one food group each keep their
/// own local stock number, updated instantly and offline, and can never be made to agree on
/// a single "true" total at every instant — each only knows its own count between sync beats.
/// But they can always agree on how much moved, regardless of the order in which those
/// movements are heard about: "-1, -1, +5" reaches the same answer whichever order it
/// arrives in, where two competing absolute totals racing each other do not. See
/// SyncInboxController.RelaySharedStockDeltaAsync for the other half of this - Head Office
/// relaying a branch's delta to its food-group siblings - and BranchHeartbeatService.
/// RunRelaySharedStockDeltaAsync for a sibling applying one.
///
/// A branch in no food group pays for this too (a few dozen bytes on its next sync, dwarfed
/// by the existing multi-second heartbeat traffic) rather than being special-cased out here -
/// Head Office is the one place that actually knows whether relaying anywhere is needed
/// (RelaySharedStockDeltaAsync looks up the source branch's FoodGroupId and simply returns
/// if it has none), so this stays as stateless and mechanical as SyncCapture itself.
/// </summary>
public static class SharedStockCapture
{
    public static List<SyncOutboxEntry> Collect(ChangeTracker tracker)
    {
        var entries = new List<SyncOutboxEntry>();
        if (!SyncCapture.IsEnabled) return entries;

        foreach (var entry in tracker.Entries<InventoryItem>())
        {
            // Only a genuine change to an already-known row carries a meaningful delta. A
            // brand-new item has no prior shared baseline to move away from - and every new
            // item starts at CurrentStock = 0 by convention (InventoryController.Create), so
            // there is nothing to tell anyone yet; it reaches a food group's other branches
            // through the ordinary catalogue push instead (see BranchHeartbeatController.
            // ConfigForBranchIfChangedAsync), the same way any new menu item does.
            if (entry.State != EntityState.Modified) continue;

            var stock = entry.Property(e => e.CurrentStock);
            if (!stock.IsModified) continue;

            var delta = stock.CurrentValue - stock.OriginalValue;
            if (delta == 0) continue;

            var branchId = entry.Property(e => e.BranchId).CurrentValue;
            if (branchId == Guid.Empty) continue;

            entries.Add(new SyncOutboxEntry
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                AggregateType = "inventory_stock_delta",
                AggregateId = entry.Entity.Id,
                EventType = "inventory_stock_delta.changed",
                EventData = JsonSerializer.Serialize(new
                {
                    inventoryItemId = entry.Entity.Id,
                    delta,
                }),
                CreatedAt = DateTime.UtcNow,
            });
        }

        return entries;
    }
}
