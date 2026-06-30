using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §14: Members Dashboard with wallet and loyalty points</summary>
public class Member
{
    public Guid Id { get; set; }
    public string MemberNumber { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string MobileNumber { get; set; } = null!;
    public string? Email { get; set; }
    public string? Username { get; set; }       // nullable — set when operator assigns login
    public string? PasswordHash { get; set; }   // BCrypt hash
    
    // Password Reset fields
    public string? ResetToken { get; set; }
    public DateTimeOffset? ResetTokenExpiry { get; set; }
    
    public MemberStatus Status { get; set; } = MemberStatus.Active;

    // SOP §14.1: Wallet System - Separated per SOP §11.1
    public decimal GamingBalance { get; set; }
    public decimal FoodBalance { get; set; }

    // SOP §15: Loyalty Point System — gaming/food separated
    public int GamingPoints { get; set; }
    public int FoodPoints { get; set; }
    public int TotalPoints { get; set; }

    // Spending tracking — separated per SOP
    public decimal TotalGamingSpend { get; set; }
    public decimal TotalFoodSpend { get; set; }

    public Guid? HomeBranchId { get; set; }
    public DateTimeOffset JoinDate { get; set; }
    public DateTimeOffset? LastVisit { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Branch? HomeBranch { get; set; }
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public ICollection<LoyaltyPoint> LoyaltyPoints { get; set; } = new List<LoyaltyPoint>();
}
