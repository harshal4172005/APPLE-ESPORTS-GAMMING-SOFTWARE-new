namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §9: Individual bill line items</summary>
public class BillItem
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public string ItemType { get; set; } = null!; // 'gaming' or 'food'
    public string ItemName { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public Guid? InventoryId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Bill Bill { get; set; } = null!;
}
