using System;

namespace AppleEsportsErp.Application.DTOs.Settings;

public class CreateBranchDto
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string OpeningTime { get; set; } = "10:00";
    public string ClosingTime { get; set; } = "02:00";
}

public class UpdateBranchDto
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string OpeningTime { get; set; } = null!;
    public string ClosingTime { get; set; } = null!;
}

public class BranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string OpeningTime { get; set; } = "10:00";
    public string ClosingTime { get; set; } = "02:00";
    public string Status { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateOperatorDto
{
    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public Guid BranchId { get; set; }
    public string DashboardPermissions { get; set; } = "{}";
}

public class UpdateOperatorDto
{
    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Password { get; set; } // Optional on update
    public Guid BranchId { get; set; }
    public string DashboardPermissions { get; set; } = "{}";
}

public class OperatorDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string DashboardPermissions { get; set; } = "{}";
    public bool IsGlobalAdmin { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
}

public class ManageAdminRoleDto
{
    public bool IsGlobalAdmin { get; set; }
    public bool CanAccessSettings { get; set; }
    public bool CanGiveDiscount { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public string? UserRole { get; set; }
    public string Action { get; set; } = null!;
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreatePcDto
{
    public string PcNumber { get; set; } = null!;
    public string? PcName { get; set; }
    public Guid BranchId { get; set; }
    public string? IpAddress { get; set; }
    public string? Specs { get; set; }
    public string? Zone { get; set; }
    public string? HardwareNotes { get; set; }
}

public class UpdatePcDto
{
    public string PcNumber { get; set; } = null!;
    public string? PcName { get; set; }
    public string? IpAddress { get; set; }
    public string? Specs { get; set; }
    public string? Zone { get; set; }
    public string? HardwareNotes { get; set; }
}
