using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Reports;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class ReportsService : IReportsService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReportsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ReconciliationReportDto>> GetCashReconciliationReportAsync(Guid branchId, DateTime startDate, DateTime endDate)
    {
        var startUtc = startDate.ToUniversalTime();
        var endUtc = endDate.AddDays(1).AddTicks(-1).ToUniversalTime();

        var registers = await _unitOfWork.Repository<CashRegister>().Query()
            .Include(r => r.Operator)
            .Include(r => r.DenominationCounts)
            .Where(r => r.BranchId == branchId && r.OpenedAt >= startUtc && r.OpenedAt <= endUtc)
            .OrderByDescending(r => r.OpenedAt)
            .ToListAsync();

        var reports = new List<ReconciliationReportDto>();

        foreach (var r in registers)
        {
            var denom = r.DenominationCounts.OrderByDescending(d => d.CreatedAt).FirstOrDefault();
            
            reports.Add(new ReconciliationReportDto
            {
                ShiftId = r.ShiftId,
                CashRegisterId = r.Id,
                OperatorName = r.Operator?.Username ?? "Unknown",
                Status = r.Status.ToString(),
                OpenedAt = r.OpenedAt,
                ClosedAt = r.ClosedAt,
                ExpectedDrawerCash = r.ExpectedDrawerCash,
                PhysicalCashCounted = r.PhysicalCashCounted ?? 0,
                Difference = r.CashDifference ?? 0,
                MismatchReason = r.MismatchReason ?? string.Empty,
                IsVerified = (r.CashDifference == 0),
                
                // Denominations
                Notes2000 = denom?.Notes2000 ?? 0,
                Notes500 = denom?.Notes500 ?? 0,
                Notes200 = denom?.Notes200 ?? 0,
                Notes100 = denom?.Notes100 ?? 0,
                Notes50 = denom?.Notes50 ?? 0,
                Notes20 = denom?.Notes20 ?? 0,
                Notes10 = denom?.Notes10 ?? 0,
                Coins5 = denom?.Coins5 ?? 0,
                Coins2 = denom?.Coins2 ?? 0,
                Coins1 = denom?.Coins1 ?? 0
            });
        }

        return reports;
    }
}
