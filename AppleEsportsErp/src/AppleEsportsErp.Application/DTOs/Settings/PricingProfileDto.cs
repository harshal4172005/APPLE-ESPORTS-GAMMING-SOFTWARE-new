namespace AppleEsportsErp.Application.DTOs.Settings;

public class PricingProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal BaseHourlyRate { get; set; }
    public Guid BranchId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? RefreshRate { get; set; }
    public string? SystemSpecs { get; set; }
}

public class CreatePricingProfileDto
{
    public string Name { get; set; } = null!;
    public decimal BaseHourlyRate { get; set; }
    public Guid BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RefreshRate { get; set; }
    public string? SystemSpecs { get; set; }
}

public class UpdatePricingProfileDto
{
    public string Name { get; set; } = null!;
    public decimal BaseHourlyRate { get; set; }
    public bool IsActive { get; set; }
    public string? RefreshRate { get; set; }
    public string? SystemSpecs { get; set; }
}
