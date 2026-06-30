using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §13: Menu Editor / Food Inventory</summary>
public class InventoryItem
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string ItemName { get; set; } = null!;
    public string? Category { get; set; }
    public decimal Price { get; set; }
    public int CurrentStock { get; set; }
    public int SoldQty { get; set; }
    public int MinStockLimit { get; set; } = 5;
    public FoodAvailability Status { get; set; } = FoodAvailability.Available;
    public string? ImageUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Branch Branch { get; set; } = null!;
    public ICollection<InventoryLog> Logs { get; set; } = new List<InventoryLog>();
}
