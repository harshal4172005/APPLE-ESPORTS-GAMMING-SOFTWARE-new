using System.ComponentModel.DataAnnotations;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Application.DTOs.FoodOrders;

public class FoodOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public Guid? SessionId { get; set; }
    public Guid? PcId { get; set; }
    public string? PcNumber { get; set; }
    public Guid BranchId { get; set; }
    public Guid? OperatorId { get; set; }
    public string? CustomerName { get; set; }
    
    public decimal TotalAmount { get; set; }
    public string? PaymentType { get; set; }
    public OrderStatus Status { get; set; }
    public string? CancelledReason { get; set; }
    
    public DateTimeOffset OrderTime { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    
    public List<FoodOrderItemDto> Items { get; set; } = new();
}

public class FoodOrderItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public string ItemName { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class CreateFoodOrderDto
{
    public Guid? SessionId { get; set; }
    public Guid? PcId { get; set; }
    public string? CustomerName { get; set; }
    
    [Required]
    public List<CreateFoodOrderItemDto> Items { get; set; } = new();
}

public class CreateFoodOrderItemDto
{
    [Required]
    public Guid InventoryId { get; set; }
    
    [Required]
    [Range(1, 100)]
    public int Quantity { get; set; }
}

public class UpdateOrderStatusDto
{
    [Required]
    public OrderStatus Status { get; set; }
    public string? Reason { get; set; }
}
