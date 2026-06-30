namespace AppleEsportsErp.Application.DTOs.Eod;

public class EodReportDto
{
    public Guid BranchId { get; set; }
    public DateTimeOffset ReportDate { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    
    public RevenueSummaryDto Revenue { get; set; } = new();
    public CashSummaryDto Cash { get; set; } = new();
    public PaymentMethodSummaryDto PaymentMethods { get; set; } = new();
    public ShiftSummaryDto Shifts { get; set; } = new();
    public OperationalStatsDto Operations { get; set; } = new();
}

public class RevenueSummaryDto
{
    public decimal TotalGamingRevenue { get; set; }
    public decimal TotalFoodRevenue { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal NetRevenue { get; set; }
}

public class CashSummaryDto
{
    public decimal TotalOpeningBalance { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCashInwards { get; set; }
    public decimal TotalPettyExpenses { get; set; }
    public decimal TotalOwnerWithdrawals { get; set; }
    public decimal ExpectedCashInDrawer { get; set; }
    public decimal ActualPhysicalCashCounted { get; set; }
    public decimal TotalDiscrepancy { get; set; }
}

public class PaymentMethodSummaryDto
{
    public decimal TotalCash { get; set; }
    public decimal TotalOnline { get; set; }
    public decimal TotalWalletDeductions { get; set; }
    public decimal TotalWalletTopUps { get; set; }
}

public class ShiftSummaryDto
{
    public int TotalShifts { get; set; }
    public List<ShiftDetailDto> ShiftDetails { get; set; } = new();
}

public class ShiftDetailDto
{
    public Guid ShiftId { get; set; }
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = null!;
    public decimal TotalSales { get; set; }
    public decimal CashDiscrepancy { get; set; }
}

public class OperationalStatsDto
{
    public int TotalSessions { get; set; }
    public int TotalReservations { get; set; }
    public int TotalFoodOrders { get; set; }
    public int NewMembersRegistered { get; set; }
}
