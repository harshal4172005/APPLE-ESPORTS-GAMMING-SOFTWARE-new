using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Eod;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class EodService : IEodService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public EodService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<ValidationStatusDto> GetValidationStatusAsync(Guid branchId, DateTimeOffset targetDate)
    {
        var startOfDay = new DateTimeOffset(targetDate.UtcDateTime.Date, TimeSpan.Zero);
        var endOfDay = startOfDay.AddDays(1);

        var blockers = new List<string>();

        // 1. Check for unclosed Shifts
        var unclosedShifts = await _unitOfWork.Repository<Shift>().Query()
            .Where(s => s.BranchId == branchId && s.Status != ShiftStatus.Completed)
            .CountAsync();
            
        if (unclosedShifts > 0)
            blockers.Add($"{unclosedShifts} shift(s) are not yet Completed/Closed.");

        // 2. Check for unclosed Cash Registers
        var unclosedRegisters = await _unitOfWork.Repository<CashRegister>().Query()
            .Where(r => r.BranchId == branchId && r.Status != CashRegisterStatus.Closed)
            .CountAsync();

        if (unclosedRegisters > 0)
            blockers.Add($"{unclosedRegisters} cash register(s) have not finalized end-of-shift verification.");

        // 3. Check for Pending/Unpaid Bills
        var pendingBills = await _unitOfWork.Repository<Bill>().Query()
            .Where(b => b.BranchId == branchId && b.CreatedAt >= startOfDay && b.CreatedAt < endOfDay && b.Status != BillStatus.Completed)
            .CountAsync();

        if (pendingBills > 0)
            blockers.Add($"{pendingBills} bill(s) are still pending/unpaid.");

        // 4. Check if Snapshot already exists
        var snapshotExists = await _unitOfWork.Repository<EodSnapshot>().Query()
            .AnyAsync(e => e.BranchId == branchId && e.ReportDate == startOfDay);
            
        if (snapshotExists)
            blockers.Add("End of Day has already been finalized for this date.");

        return new ValidationStatusDto
        {
            IsReady = !blockers.Any(),
            Blockers = blockers
        };
    }

    public async Task<EodReportDto> GenerateEodReportAsync(Guid branchId, DateTimeOffset targetDate)
    {
        var startOfDay = new DateTimeOffset(targetDate.UtcDateTime.Date, TimeSpan.Zero);
        var endOfDay = startOfDay.AddDays(1);

        // Fetch Bills
        var bills = await _unitOfWork.Repository<Bill>().Query()
            .Where(b => b.BranchId == branchId && b.Status == BillStatus.Completed && b.CompletedAt >= startOfDay && b.CompletedAt < endOfDay)
            .ToListAsync();
        var completedBills = bills;

        // Fetch Payments
        var payments = await _unitOfWork.Repository<Payment>().Query()
            .Where(p => p.BranchId == branchId && p.CreatedAt >= startOfDay && p.CreatedAt < endOfDay)
            .ToListAsync();

        // Fetch Registers
        var registers = await _unitOfWork.Repository<CashRegister>().Query()
            .Include(r => r.Operator)
            .Include(r => r.CashTransactions)
            .Where(r => r.BranchId == branchId && (
                (r.OpenedAt >= startOfDay && r.OpenedAt < endOfDay) || 
                (r.ClosedAt >= startOfDay && r.ClosedAt < endOfDay) || 
                r.Status == CashRegisterStatus.Open
            ))
            .ToListAsync();

        var walletTxs = await _unitOfWork.Repository<WalletTransaction>().Query()
            .Where(w => w.BranchId == branchId && w.CreatedAt >= startOfDay && w.CreatedAt < endOfDay)
            .ToListAsync();

        var report = new EodReportDto
        {
            BranchId = branchId,
            ReportDate = startOfDay,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        // Revenue (Gaming / Food)
        report.Revenue.TotalGamingRevenue = completedBills.Sum(b => b.GamingAmount);
        report.Revenue.TotalFoodRevenue = completedBills.Sum(b => b.FoodAmount);
        report.Revenue.TotalDiscounts = completedBills.Sum(b => b.DiscountAmount);
        report.Revenue.NetRevenue = completedBills.Sum(b => b.TotalAmount);

        // Payment Methods
        report.PaymentMethods.TotalCash = payments.Sum(p => p.CashAmount);
        report.PaymentMethods.TotalOnline = payments.Sum(p => p.OnlineAmount);
        report.PaymentMethods.TotalWalletDeductions = payments.Sum(p => p.WalletAmount);
        report.PaymentMethods.TotalWalletTopUps = walletTxs.Where(w => w.Action == WalletAction.Recharge).Sum(w => w.Amount);

        // Cash Summary
        report.Cash.TotalOpeningBalance = registers.Sum(r => r.OpeningBalance);
        report.Cash.TotalCashSales = registers.Sum(r => r.TotalCashSales) + report.PaymentMethods.TotalWalletTopUps;
        
        var allCashTxs = registers.SelectMany(r => r.CashTransactions).ToList();
        report.Cash.TotalCashInwards = allCashTxs.Where(t => t.TransactionType == "inward").Sum(t => t.CashAmount);
        report.Cash.TotalPettyExpenses = allCashTxs.Where(t => t.TransactionType == "petty_expense").Sum(t => Math.Abs(t.CashAmount));
        report.Cash.TotalOwnerWithdrawals = allCashTxs.Where(t => t.TransactionType == "withdrawal").Sum(t => Math.Abs(t.CashAmount));
        
        report.Cash.ExpectedCashInDrawer = registers.Sum(r => r.ExpectedDrawerCash);
        report.Cash.ActualPhysicalCashCounted = registers.Sum(r => r.PhysicalCashCounted ?? 0);
        report.Cash.TotalDiscrepancy = registers.Sum(r => r.CashDifference ?? 0);

        // Shifts Summary
        report.Shifts.TotalShifts = registers.Count;
        foreach (var reg in registers)
        {
            report.Shifts.ShiftDetails.Add(new ShiftDetailDto
            {
                ShiftId = reg.ShiftId,
                OperatorId = reg.OperatorId,
                OperatorName = reg.Operator?.FullName ?? "Unknown",
                TotalSales = reg.TotalCashSales,
                CashDiscrepancy = reg.CashDifference ?? 0
            });
        }

        // Operational Stats
        report.Operations.TotalSessions = await _unitOfWork.Repository<Session>().Query().CountAsync(s => s.BranchId == branchId && s.StartTime >= startOfDay && s.StartTime < endOfDay);
        report.Operations.TotalReservations = await _unitOfWork.Repository<Reservation>().Query().CountAsync(r => r.BranchId == branchId && r.CreatedAt >= startOfDay && r.CreatedAt < endOfDay);
        report.Operations.TotalFoodOrders = await _unitOfWork.Repository<FoodOrder>().Query().CountAsync(o => o.BranchId == branchId && o.CreatedAt >= startOfDay && o.CreatedAt < endOfDay);
        report.Operations.NewMembersRegistered = await _unitOfWork.Repository<Member>().Query().CountAsync(m => m.HomeBranchId == branchId && m.CreatedAt >= startOfDay && m.CreatedAt < endOfDay);

        // Credit Logs
        var credits = await _unitOfWork.Repository<CustomerCredit>().Query()
            .Where(c => c.BranchId == branchId && ((c.CreatedAt >= startOfDay && c.CreatedAt < endOfDay) || (c.ClearedAt >= startOfDay && c.ClearedAt < endOfDay)))
            .ToListAsync();

        report.CreditLogs = credits.Select(c => new EodCreditLogDto
        {
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
        }).ToList();

        return report;
    }

    public async Task<EodSnapshotDto> FinalizeEodAsync(Guid branchId, Guid operatorId, DateTimeOffset targetDate)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var startOfDay = new DateTimeOffset(targetDate.UtcDateTime.Date, TimeSpan.Zero);
            
            // 1. Verify Validation Status
            var status = await GetValidationStatusAsync(branchId, startOfDay);
            if (!status.IsReady)
            {
                throw new AppException("Cannot finalize EOD due to unresolved blockers: " + string.Join("; ", status.Blockers));
            }

            // 2. Generate dynamic report payload
            var report = await GenerateEodReportAsync(branchId, startOfDay);

            // 3. Serialize securely using JSON
            var jsonData = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = false });

            // 4. Create immutable snapshot
            var snapshot = new EodSnapshot
            {
                BranchId = branchId,
                ReportDate = startOfDay,
                GeneratedByOperatorId = operatorId,
                SnapshotVersion = 1,
                SchemaVersion = "B.8-v1",
                SnapshotData = jsonData,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<EodSnapshot>().AddAsync(snapshot);

            // 5. Audit Log
            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "SuperAdmin", // Must be super admin
                UserName = "System",
                Action = "eod_finalize",
                BranchId = branchId,
                TargetType = "eod_snapshot",
                TargetId = snapshot.Id,
                Details = new { SchemaVersion = snapshot.SchemaVersion, Revenue = report.Revenue.NetRevenue }
            });

            await _unitOfWork.CommitTransactionAsync();

            return new EodSnapshotDto
            {
                Id = snapshot.Id,
                BranchId = snapshot.BranchId,
                ReportDate = snapshot.ReportDate,
                GeneratedByOperatorId = snapshot.GeneratedByOperatorId,
                SnapshotVersion = snapshot.SnapshotVersion,
                SchemaVersion = snapshot.SchemaVersion,
                CreatedAt = snapshot.CreatedAt,
                Data = report
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<EodSnapshotDto?> GetHistoricalEodAsync(Guid branchId, DateTimeOffset targetDate)
    {
        var startOfDay = new DateTimeOffset(targetDate.UtcDateTime.Date, TimeSpan.Zero);
        var snapshot = await _unitOfWork.Repository<EodSnapshot>().Query()
            .FirstOrDefaultAsync(e => e.BranchId == branchId && e.ReportDate == startOfDay);

        if (snapshot == null) return null;

        var deserializedData = JsonSerializer.Deserialize<EodReportDto>(snapshot.SnapshotData);

        return new EodSnapshotDto
        {
            Id = snapshot.Id,
            BranchId = snapshot.BranchId,
            ReportDate = snapshot.ReportDate,
            GeneratedByOperatorId = snapshot.GeneratedByOperatorId,
            SnapshotVersion = snapshot.SnapshotVersion,
            SchemaVersion = snapshot.SchemaVersion,
            CreatedAt = snapshot.CreatedAt,
            Data = deserializedData!
        };
    }
}
