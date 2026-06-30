namespace AppleEsportsErp.Domain.Entities;

/// <summary>SOP §15: Point tracking — gaming/food separated</summary>
public class LoyaltyPoint
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? OperatorId { get; set; }
    public Guid? AdminId { get; set; }
    public string Action { get; set; } = null!; // earn_gaming, earn_food, redeem, add_manual, remove_manual, correction
    public string Category { get; set; } = null!; // gaming, food, both
    public int Points { get; set; }
    public int PointsBefore { get; set; }
    public int PointsAfter { get; set; }
    public Guid? BillId { get; set; }
    public string? RewardType { get; set; }
    public decimal? RewardValue { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public Operator? Operator { get; set; }
    public User? Admin { get; set; }
    public Bill? Bill { get; set; }
}
