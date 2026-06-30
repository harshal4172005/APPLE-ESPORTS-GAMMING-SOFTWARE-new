using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §9.6: Discount records — Super Admin Only</summary>
public class Discount
{
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    public Guid BranchId { get; set; }
    public Guid AdminId { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public string Reason { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Bill Bill { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public User Admin { get; set; } = null!;
}
