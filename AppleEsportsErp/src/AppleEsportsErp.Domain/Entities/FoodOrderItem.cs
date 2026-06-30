namespace AppleEsportsErp.Domain.Entities;

/// <summary>Individual food order line items</summary>
public class FoodOrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid InventoryId { get; set; }
    public string ItemName { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public FoodOrder FoodOrder { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
}
