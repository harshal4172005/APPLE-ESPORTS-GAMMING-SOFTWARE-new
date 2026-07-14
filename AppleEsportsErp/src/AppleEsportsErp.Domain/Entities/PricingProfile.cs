namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP PC Management: Dynamic pricing architecture</summary>
public class PricingProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal BaseHourlyRate { get; set; }
    public Guid BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string? RefreshRate { get; set; }
    public string? SystemSpecs { get; set; }
    
    // Navigation
    public Branch Branch { get; set; } = null!;
    public ICollection<Pc> Pcs { get; set; } = new List<Pc>();
}
