using AppleEsportsErp.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace AppleEsportsErp.Application.DTOs.PcManagement;

public class PcDto
{
    public Guid Id { get; set; }
    public string PcNumber { get; set; } = null!;
    public string? PcName { get; set; }
    public string? Zone { get; set; }
    public Guid BranchId { get; set; }
    public PcState State { get; set; }
    public Guid? PricingProfileId { get; set; }
    public string? PricingProfileName { get; set; }
    public string? HardwareNotes { get; set; }
    public string? MonitorHz { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

public class CreatePcDto
{
    [Required]
    public string PcNumber { get; set; } = null!;
    public string? PcName { get; set; }
    public string? Zone { get; set; }
    public Guid? PricingProfileId { get; set; }
    public string? HardwareNotes { get; set; }
    public string? MonitorHz { get; set; }
}

public class UpdatePcDto
{
    [Required]
    public string PcNumber { get; set; } = null!;
    public string? PcName { get; set; }
    public string? Zone { get; set; }
    public Guid? PricingProfileId { get; set; }
    public string? HardwareNotes { get; set; }
    public string? MonitorHz { get; set; }
    public bool IsActive { get; set; }
}
