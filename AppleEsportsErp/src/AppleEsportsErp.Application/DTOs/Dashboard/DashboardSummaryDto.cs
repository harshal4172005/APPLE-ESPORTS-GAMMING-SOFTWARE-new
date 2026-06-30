namespace AppleEsportsErp.Application.DTOs.Dashboard;

public class DashboardSummaryDto
{
    // Realtime Operational Metrics
    public int TotalActiveSessions { get; set; }
    public int TotalActivePcs { get; set; }
    public int ReservedPcs { get; set; }
    public int PcsUnderMaintenance { get; set; }
    public int AwaitingBillingPcs { get; set; }
    public int ActiveFoodOrders { get; set; }
    public int ActiveOperators { get; set; }
    public int LowStockAlerts { get; set; }

    // Cached Financial Metrics (Today)
    public int TodayBillsCount { get; set; }
    public decimal TotalRevenueToday { get; set; }
    public decimal GamingRevenueToday { get; set; }
    public decimal FoodRevenueToday { get; set; }
    public decimal CashTotals { get; set; }
    public decimal OnlineTotals { get; set; }
    public decimal WalletTotals { get; set; }
}
