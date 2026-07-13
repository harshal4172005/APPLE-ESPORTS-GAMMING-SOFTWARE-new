using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §5.1: Super Admin accounts — highest authority level</summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = "super_admin";
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? LastLogin { get; set; }
    public string? DeviceInfo { get; set; } // JSONB stored as string
    public string? DashboardPermissions { get; set; } // JSONB stored as string for Admins
    public string? AccessPin { get; set; } // SOP §22: Admin Quick-Switch PIN
    
    // Password Reset fields
    public string? ResetToken { get; set; }
    public DateTimeOffset? ResetTokenExpiry { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
