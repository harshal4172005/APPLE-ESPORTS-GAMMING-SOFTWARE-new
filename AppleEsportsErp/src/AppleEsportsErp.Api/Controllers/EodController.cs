using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Eod;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Constants;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/eod")]
[Authorize]
[BranchIsolation]
public class EodController : ControllerBase
{
    private readonly IEodService _eodService;
    private readonly IUnitOfWork _unitOfWork;

    public EodController(IEodService eodService, IUnitOfWork unitOfWork)
    {
        _eodService = eodService;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("range-report")]
    public async Task<IActionResult> GetRangeReport(
        [FromQuery] DateTimeOffset? startDate, 
        [FromQuery] DateTimeOffset? endDate, 
        [FromQuery] Guid? branchId = null)
    {
        var targetBranchId = branchId ?? GetBranchId();
        
        var startUtc = (startDate ?? DateTimeOffset.UtcNow.AddDays(-30)).ToUniversalTime();
        var endUtc = (endDate ?? DateTimeOffset.UtcNow).ToUniversalTime();

        var bills = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Bill>()
            .Query()
            .Include(b => b.DiscountByAdmin)
            .Include(b => b.Operator)
            .Include(b => b.Session)
            .Where(b => b.BranchId == targetBranchId 
                     && b.Status == AppleEsportsErp.Domain.Enums.BillStatus.Completed 
                     && b.CompletedAt >= startUtc 
                     && b.CompletedAt <= endUtc)
            .ToListAsync();

        var dailyReport = bills
            .GroupBy(b => b.CompletedAt!.Value.Date)
            .Select(g => new {
                Date = g.Key.ToString("yyyy-MM-dd"),
                GamingRevenue = g.Sum(b => b.Subtotal > 0 ? b.GamingAmount - (b.GamingAmount / b.Subtotal * b.DiscountAmount) : b.GamingAmount),
                FoodRevenue = g.Sum(b => b.Subtotal > 0 ? b.FoodAmount - (b.FoodAmount / b.Subtotal * b.DiscountAmount) : b.FoodAmount),
                DiscountAmount = g.Sum(b => b.DiscountAmount),
                TotalRevenue = g.Sum(b => b.TotalAmount)
            })
            .OrderBy(r => r.Date)
            .ToList();

        var monthlyReport = bills
            .GroupBy(b => new { b.CompletedAt!.Value.Year, b.CompletedAt!.Value.Month })
            .Select(g => new {
                Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                GamingRevenue = g.Sum(b => b.Subtotal > 0 ? b.GamingAmount - (b.GamingAmount / b.Subtotal * b.DiscountAmount) : b.GamingAmount),
                FoodRevenue = g.Sum(b => b.Subtotal > 0 ? b.FoodAmount - (b.FoodAmount / b.Subtotal * b.DiscountAmount) : b.FoodAmount),
                DiscountAmount = g.Sum(b => b.DiscountAmount),
                TotalRevenue = g.Sum(b => b.TotalAmount)
            })
            .OrderBy(r => r.Month)
            .ToList();

        var discountAudit = bills
            .Where(b => b.DiscountAmount > 0)
            .Select(b => new {
                BillId = b.BillNumber,
                Date = b.CompletedAt,
                Subtotal = b.Subtotal,
                DiscountAmount = b.DiscountAmount,
                DiscountType = b.DiscountType?.ToString(),
                DiscountValue = b.DiscountValue,
                DiscountReason = b.DiscountReason,
                GivenBy = b.DiscountByAdmin != null 
                    ? $"Super Admin ({b.DiscountByAdmin.FullName})" 
                    : (b.Operator != null ? $"Operator ({b.Operator.FullName})" : "Unknown")
            })
            .OrderByDescending(d => d.Date)
            .ToList();

        var billIds = bills.Select(b => b.Id).ToList();
        var billCredits = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.CustomerCredit>()
            .Query()
            .Where(c => billIds.Contains(c.BillId))
            .ToListAsync();

        var allBills = bills.Select(b => {
            var credit = billCredits.FirstOrDefault(c => c.BillId == b.Id);
            var actualPaid = b.CashAmount + b.OnlineAmount + b.WalletAmount;
            var isCredit = credit != null || actualPaid < b.TotalAmount || b.PaymentType?.ToString() == "Credit";
            
            return new {
                BillId = b.BillNumber,
                Date = b.CompletedAt,
                Operator = b.Operator != null ? b.Operator.FullName : "Unknown",
                Customer = string.IsNullOrEmpty(b.CustomerName) ? "Walk-in" : b.CustomerName,
                GamingRevenue = b.GamingAmount,
                FoodRevenue = b.FoodAmount,
                Discount = b.DiscountAmount,
                TotalRevenue = b.TotalAmount,
                PaymentType = isCredit ? "Credit" : (b.PaymentType?.ToString() ?? "Unknown"),
                AmountPaidInitially = credit != null ? credit.AmountPaidInitially : actualPaid,
                CreditAmount = credit != null ? credit.CreditAmount : (isCredit ? Math.Max(0, b.TotalAmount - actualPaid) : 0),
                CreditStatus = isCredit ? "pending" : null,
                SessionNotes = b.Session?.Notes,
                SessionStartTime = b.Session != null ? b.Session.StartTime : (DateTimeOffset?)null,
                SessionEndTime = b.Session != null ? b.Session.EndTime : (DateTimeOffset?)null,
                SessionDurationMinutes = b.Session != null && b.Session.EndTime.HasValue 
                    ? (b.Session.EndTime.Value - b.Session.StartTime).TotalMinutes 
                    : 0,
                PcId = b.PcId,
                PcName = b.Pc != null ? (b.Pc.PcName ?? b.Pc.PcNumber.ToString()) : "Walk-in"
            };
        })
        .OrderByDescending(b => b.Date)
        .ToList();

        var credits = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.CustomerCredit>()
            .Query()
            .Include(c => c.Bill)
            .Include(c => c.ClearedByOperator)
            .Where(c => c.BranchId == targetBranchId 
                     && ((c.CreatedAt >= startUtc && c.CreatedAt <= endUtc) || (c.ClearedAt >= startUtc && c.ClearedAt <= endUtc)))
            .ToListAsync();

        var clearedPastCredits = credits
            .Where(c => c.Status != null && c.Status.ToLower() == "cleared" && c.ClearedAt >= startUtc && c.ClearedAt <= endUtc)
            .Select(c => new {
                BillId = $"SETTLED-{(c.Bill != null ? c.Bill.BillNumber : "CREDIT")}",
                Date = c.ClearedAt,
                Operator = c.ClearedByOperator != null ? c.ClearedByOperator.FullName : "Unknown",
                Customer = string.IsNullOrEmpty(c.CustomerName) ? "Walk-in" : c.CustomerName,
                GamingRevenue = 0m,
                FoodRevenue = 0m,
                Discount = 0m,
                TotalRevenue = c.CreditAmount,
                PaymentType = "CREDIT SETTLED",
                AmountPaidInitially = c.AmountPaidInitially,
                CreditAmount = c.CreditAmount,
                CreditStatus = "cleared",
                SessionNotes = "Credit clearance payment for past session",
                SessionStartTime = (DateTimeOffset?)null,
                SessionEndTime = (DateTimeOffset?)null,
                SessionDurationMinutes = 0d,
                PcId = (Guid?)null,
                PcName = c.PcNumber ?? "N/A"
            })
            .Cast<object>()
            .ToList();

        var allBillsList = allBills.Cast<object>().ToList();
        var combinedBills = allBillsList.Concat(clearedPastCredits)
            .OrderByDescending(b => ((dynamic)b).Date)
            .ToList();

        var allCredits = credits.Select(c => new {
            CreditId = c.Id,
            CustomerName = c.CustomerName,
            CustomerPhone = c.CustomerPhone,
            PcNumber = c.PcNumber,
            OriginalBillAmount = c.OriginalBillAmount,
            AmountPaidInitially = c.AmountPaidInitially,
            CreditAmount = c.CreditAmount,
            Status = c.Status,
            CreatedAt = c.CreatedAt,
            ClearedAt = c.ClearedAt
        })
        .OrderByDescending(c => c.CreatedAt)
        .ToList();

        return Ok(ApiResponse<object>.Ok(new {
            Daily = dailyReport,
            Monthly = monthlyReport,
            Discounts = discountAudit,
            AllBills = combinedBills,
            AllCredits = allCredits
        }));
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet("report")]
    [HttpGet("preview")]
    public async Task<IActionResult> GetPreview([FromQuery] DateTimeOffset? date)
    {
        var targetDate = (date ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var result = await _eodService.GenerateEodReportAsync(GetBranchId(), targetDate);
        return Ok(ApiResponse<EodReportDto>.Ok(result));
    }

    [HttpGet("validation")]
    public async Task<IActionResult> GetValidationStatus([FromQuery] DateTimeOffset? date)
    {
        var targetDate = (date ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var result = await _eodService.GetValidationStatusAsync(GetBranchId(), targetDate);
        return Ok(ApiResponse<ValidationStatusDto>.Ok(result));
    }

    [HttpPost("finalize")]
    [Authorize(Roles = Roles.SuperAdmin)] // Strictly SuperAdmin as per SOP
    public async Task<IActionResult> FinalizeEod([FromBody] FinalizeEodRequest request)
    {
        var targetDate = (request.Date ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var result = await _eodService.FinalizeEodAsync(GetBranchId(), (await this.GetOperatorIdAsync()), targetDate);
        return Ok(ApiResponse<EodSnapshotDto>.Ok(result));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistoricalEod([FromQuery] DateTimeOffset date)
    {
        var result = await _eodService.GetHistoricalEodAsync(GetBranchId(), date.ToUniversalTime());
        if (result == null) return NotFound(ApiResponse<EodSnapshotDto>.Fail("No finalized EOD snapshot found for the specified date."));
        return Ok(ApiResponse<EodSnapshotDto>.Ok(result));
    }
}

public class FinalizeEodRequest
{
    public DateTimeOffset? Date { get; set; }
}

