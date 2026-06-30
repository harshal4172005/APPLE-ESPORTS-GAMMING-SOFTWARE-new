namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §17: System Configuration Settings</summary>
public class SystemConfig
{
    public Guid Id { get; set; }
    public string ConfigKey { get; set; } = null!;
    public string ConfigValue { get; set; } = null!; // JSONB
    public string? Description { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public User? UpdatedByAdmin { get; set; }
}
