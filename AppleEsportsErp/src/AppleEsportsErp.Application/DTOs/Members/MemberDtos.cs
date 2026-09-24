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
    public decimal TotalGamingTopUps { get; set; }
    public decimal TotalGamingBonusEarned { get; set; }
    public decimal TotalGamingSpend { get; set; }
    public decimal TotalFoodSpend { get; set; }
    public int GamingPoints { get; set; }
    public int FoodPoints { get; set; }
    public int TotalPoints { get; set; }

    public DateTimeOffset JoinDate { get; set; }
    public DateTimeOffset? LastVisit { get; set; }

    public string? HomeBranchName { get; set; }
}

/// <summary>Super Admin only: direct override of any value on a member's profile.
/// Every field is optional — only the ones provided get changed. Balance changes
/// (Gaming/Food) also create a "Correction" wallet transaction so there's an audit trail.</summary>
public class AdminEditMemberValuesDto
{
    public decimal? GamingBalance { get; set; }
    public decimal? FoodBalance { get; set; }
    public decimal? TotalGamingTopUps { get; set; }
    public decimal? TotalGamingBonusEarned { get; set; }
    public decimal? TotalGamingSpend { get; set; }
    public decimal? TotalFoodSpend { get; set; }
    public int? GamingPoints { get; set; }
    public int? FoodPoints { get; set; }
    public int? TotalPoints { get; set; }
    public string? Reason { get; set; }
}

/// <summary>One row of a member's combined history - a gaming session or a wallet
/// top-up/deduction, whichever it is. PcName/DurationMinutes are set only for a
/// Session row.</summary>
public class MemberHistoryEntryDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!; // "Session" | "WalletTopUp" | "WalletDeduction"
    public DateTimeOffset Timestamp { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public string? PcName { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;
}

public class RegisterMemberDto
{
    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Invalid mobile number format.")]
    public string MobileNumber { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

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
