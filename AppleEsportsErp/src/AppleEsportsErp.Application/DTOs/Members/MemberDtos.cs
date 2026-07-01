using System.ComponentModel.DataAnnotations;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Application.DTOs.Members;

public class MemberDto
{
    public Guid Id { get; set; }
    public string MemberNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string MobileNumber { get; set; } = null!;
    public string? Email { get; set; }
    public string? Username { get; set; }
    public bool HasPassword { get; set; }
    public MemberStatus Status { get; set; }

    public decimal GamingBalance { get; set; }
    public decimal FoodBalance { get; set; }
    public int GamingPoints { get; set; }
    public int FoodPoints { get; set; }
    public int TotalPoints { get; set; }

    public DateTimeOffset JoinDate { get; set; }
    public DateTimeOffset? LastVisit { get; set; }
}

public class RegisterMemberDto
{
    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Invalid mobile number format.")]
    public string MobileNumber { get; set; } = null!;

    [EmailAddress]
    public string? Email { get; set; }

    // Optional login credentials — set by operator at registration
    [StringLength(30, MinimumLength = 3)]
    public string? Username { get; set; }

    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; set; }
}

public class UpdateMemberDto
{
    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Invalid mobile number format.")]
    public string MobileNumber { get; set; } = null!;

    [EmailAddress]
    public string? Email { get; set; }

    // Optional — only updated when non-null/non-empty
    [StringLength(30, MinimumLength = 3)]
    public string? Username { get; set; }

    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; set; }

    public bool? DisableLogin { get; set; }
}

public class MemberLoginDto
{
    [Required]
    public string Identifier { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}

public class MemberLoginResponseDto
{
    public Guid MemberId { get; set; }
    public string MemberNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public decimal GamingBalance { get; set; }
    public decimal FoodBalance { get; set; }
    public string Token { get; set; } = null!;
}
