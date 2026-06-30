using System.ComponentModel.DataAnnotations;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Application.DTOs.Billing;

public class BillDto
{
    public Guid Id { get; set; }
    public string BillNumber { get; set; } = null!;
    public Guid? SessionId { get; set; }
    public Guid? PcId { get; set; }
    public string? PcNumber { get; set; }
    public Guid BranchId { get; set; }
    public Guid OperatorId { get; set; }
    public Guid? ShiftId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? MemberId { get; set; }

    public decimal GamingAmount { get; set; }
    public decimal FoodAmount { get; set; }
    public decimal Subtotal { get; set; }

    public DiscountType? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }

    public decimal TotalAmount { get; set; }

    public BillStatus Status { get; set; }
    public bool IsDeferred { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SessionEndTime { get; set; }

    public List<BillItemDto> Items { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}

public class BillItemDto
{
    public Guid Id { get; set; }
    public string ItemType { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public PaymentType PaymentType { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public decimal WalletAmount { get; set; }
    public decimal CashReceived { get; set; }
    public decimal ChangeReturned { get; set; }
    public decimal ActualCashCollected { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ApplyDiscountDto
{
    [Required]
    public DiscountType DiscountType { get; set; }
    
    [Required]
    [Range(0, 1000000)]
    public decimal DiscountValue { get; set; }
    
    [Required]
    public string Reason { get; set; } = null!;
}

public class ProcessPaymentDto
{
    [Required]
    public PaymentType PaymentType { get; set; }
    
    public decimal CashAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public decimal WalletAmount { get; set; }
    
    // Specifically for cash payments
    public decimal CashReceived { get; set; }

    public Guid? MemberId { get; set; }
}
