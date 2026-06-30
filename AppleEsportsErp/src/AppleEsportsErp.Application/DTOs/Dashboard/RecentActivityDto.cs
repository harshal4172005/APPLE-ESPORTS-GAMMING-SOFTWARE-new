namespace AppleEsportsErp.Application.DTOs.Dashboard;

public class RecentActivityDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // e.g., "SessionStarted", "PaymentCompleted", "FoodOrderPlaced"
    public string Description { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? PaymentMethod { get; set; } // Cash, Online, Wallet
    public string Category { get; set; } = string.Empty; // Operational, Financial, Gaming, Food
    public string? OperatorName { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime Timestamp { get; set; }
}
