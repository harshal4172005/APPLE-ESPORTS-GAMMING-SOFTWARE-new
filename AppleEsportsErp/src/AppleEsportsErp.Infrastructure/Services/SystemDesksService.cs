using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Services;
using AppleEsportsErp.Application.DTOs.SystemDesks;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class SystemDesksService : ISystemDesksService
{
    private readonly IUnitOfWork _unitOfWork;

    public SystemDesksService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<OnlineDeskSummaryDto> GetActiveOnlineDeskAsync(
        Guid branchId, Guid shiftId, DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var shift = await _unitOfWork.Repository<Shift>().Query()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.BranchId == branchId);

        if (shift == null)
            throw new Exception("Shift not found.");

        // Money is reported per TRADING DAY (06:00-06:00 IST), not per operator login.
        //
        // Two separate faults came from scoping it by shift. Bills were selected by their own
        // CreatedAt, so a session opened before midnight and settled after — routine at a
        // branch trading past 02:00 — landed on the previous shift and the operator who took
        // the money saw Rs 0. And an operator who logs in three times in a day used to split
        // one day's takings into three sets of figures that reconcile against nothing.
        //
        // How often somebody logs in is their business. The day's money is the day's money.
        //
        // Overridable to a chosen day or range, the same as Wallet Desk - no dates given still
        // means today, unchanged.
        var resolvedFrom = fromDate ?? IndiaTime.BusinessDayOf(DateTimeOffset.UtcNow);
        var resolvedTo = toDate ?? fromDate ?? IndiaTime.BusinessDayOf(DateTimeOffset.UtcNow);
        var (dayStart, _) = IndiaTime.BusinessDayRange(resolvedFrom);
        var (_, dayEnd) = IndiaTime.BusinessDayRange(resolvedTo);

        var payments = await _unitOfWork.Repository<Payment>().Query()
            .Where(p => p.BranchId == branchId
                     && p.OnlineAmount > 0
                     && p.CreatedAt >= dayStart
                     && p.CreatedAt < dayEnd)
            .Include(p => p.Bill)
                .ThenInclude(b => b.Member)
            .ToListAsync();

        var walletTxs = await _unitOfWork.Repository<WalletTransaction>().Query()
            .Where(w => w.BranchId == branchId && w.CreatedAt >= dayStart && w.CreatedAt < dayEnd)
            .Include(w => w.Member)
            .ToListAsync();

        var dto = new OnlineDeskSummaryDto
        {
            ShiftId = shiftId
        };

        foreach (var payment in payments)
        {
            dto.TotalOnlineSales += payment.OnlineAmount;
            dto.Transactions.Add(new OnlineTransactionDto
            {
                Id = payment.Id,
                Timestamp = payment.CreatedAt,
                Description = $"Bill Payment #{payment.Bill?.BillNumber} " +
                              $"({payment.Bill?.CustomerName ?? payment.Bill?.Member?.Username ?? "Walk-in"})",
                Amount = payment.OnlineAmount,
                PaymentMethod = "Online"
            });
        }

        foreach (var tx in walletTxs)
        {
            if (tx.OnlineAmount > 0)
            {
                dto.TotalOnlineSales += tx.OnlineAmount;
                dto.Transactions.Add(new OnlineTransactionDto
                {
                    Id = tx.Id,
                    Timestamp = tx.CreatedAt,
                    Description = $"Wallet {tx.Action} - {tx.TargetWallet} ({tx.Member?.Username ?? "Member"})",
                    Amount = tx.OnlineAmount,
                    PaymentMethod = "Online"
                });
            }
        }

        dto.Transactions = dto.Transactions.OrderByDescending(t => t.Timestamp).ToList();

        return dto;
    }

    public async Task<CashDeskSummaryDto> GetActiveCashDeskAsync(
        Guid branchId, Guid shiftId, DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var shift = await _unitOfWork.Repository<Shift>().Query()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.BranchId == branchId);

        if (shift == null)
            throw new Exception("Shift not found.");

        // Same trading-day scope as the other desks - see GetActiveOnlineDeskAsync's note. Not
        // scoped to whichever register happens to be open right now, deliberately: a register
        // closes when its shift ends, but the cash it moved should stay just as easy to find the
        // day after as it was the minute it happened.
        var resolvedFrom = fromDate ?? IndiaTime.BusinessDayOf(DateTimeOffset.UtcNow);
        var resolvedTo = toDate ?? fromDate ?? IndiaTime.BusinessDayOf(DateTimeOffset.UtcNow);
        var (dayStart, _) = IndiaTime.BusinessDayRange(resolvedFrom);
        var (_, dayEnd) = IndiaTime.BusinessDayRange(resolvedTo);

        var cashTxs = await _unitOfWork.Repository<CashTransaction>().Query()
            .Where(t => t.BranchId == branchId && t.CreatedAt >= dayStart && t.CreatedAt < dayEnd)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var dto = new CashDeskSummaryDto
        {
            ShiftId = shiftId,
            FromDate = resolvedFrom,
            ToDate = resolvedTo,
        };

        foreach (var tx in cashTxs)
        {
            dto.TotalCashSales += tx.CashAmount;
            dto.Transactions.Add(new AppleEsportsErp.Application.DTOs.Cash.CashTransactionDto
            {
                Id = tx.Id,
                BillId = tx.BillId,
                PcNumber = tx.PcNumber,
                CashAmount = tx.CashAmount,
                CashReceived = tx.CashReceived,
                ChangeReturned = tx.ChangeReturned,
                ActualCashCollected = tx.ActualCashCollected,
                GamingAmount = tx.GamingAmount,
                FoodAmount = tx.FoodAmount,
                TransactionType = tx.TransactionType,
                CustomerName = tx.CustomerName,
                CreatedAt = tx.CreatedAt,
            });
        }

        return dto;
    }

    public async Task<WalletDeskSummaryDto> GetActiveWalletDeskAsync(
        Guid branchId, Guid shiftId, DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var shift = await _unitOfWork.Repository<Shift>().Query()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.BranchId == branchId);

        if (shift == null)
            throw new Exception("Shift not found.");

        // Same trading-day scope as the Online Desk — see the note there — but overridable: an
        // operator looking back at a previous day, or a range, needs the same figures for that
        // window instead of always today's. No dates given still means today, unchanged.
        var resolvedFrom = fromDate ?? IndiaTime.BusinessDayOf(DateTimeOffset.UtcNow);
        var resolvedTo = toDate ?? fromDate ?? IndiaTime.BusinessDayOf(DateTimeOffset.UtcNow);
        var (dayStart, _) = IndiaTime.BusinessDayRange(resolvedFrom);
        var (_, dayEnd) = IndiaTime.BusinessDayRange(resolvedTo);

        var walletTxs = await _unitOfWork.Repository<WalletTransaction>().Query()
            .Where(w => w.BranchId == branchId && w.CreatedAt >= dayStart && w.CreatedAt < dayEnd)
            .Include(w => w.Member)
            .Include(w => w.Bill)
                .ThenInclude(b => b!.Pc)
            .Include(w => w.Bill)
                .ThenInclude(b => b!.Session)
            .ToListAsync();

        var walletPayments = await _unitOfWork.Repository<Payment>().Query()
            .Where(p => p.BranchId == branchId
                     && p.WalletAmount > 0
                     && p.CreatedAt >= dayStart
                     && p.CreatedAt < dayEnd)
            .Include(p => p.Bill)
                .ThenInclude(b => b.Member)
            .Include(p => p.Bill)
                .ThenInclude(b => b.Pc)
            .Include(p => p.Bill)
                .ThenInclude(b => b.Session)
            .ToListAsync();

        var dto = new WalletDeskSummaryDto
        {
            ShiftId = shiftId,
            FromDate = resolvedFrom,
            ToDate = resolvedTo,
        };

        foreach (var tx in walletTxs)
        {
            if (tx.Action == WalletAction.Recharge)
            {
                dto.TotalWalletTopUps += tx.Amount;
            }
            else if (tx.Action == WalletAction.DeductionGaming || tx.Action == WalletAction.DeductionFood)
            {
                dto.TotalWalletDeductions += tx.Amount;
            }

            dto.Transactions.Add(new WalletTransactionSummaryDto
            {
                Id = tx.Id,
                Timestamp = tx.CreatedAt,
                Description = $"Wallet {tx.Action} - {tx.TargetWallet} ({tx.Member?.Username ?? "Member"}) " + (string.IsNullOrEmpty(tx.Reason) ? "" : $"({tx.Reason})"),
                Amount = tx.Amount,
                Action = tx.Action.ToString(),
                PcName = tx.Bill?.Pc?.PcName ?? tx.Bill?.Pc?.PcNumber,
                DurationMinutes = tx.Bill?.Session?.ActualDurationMin ?? tx.Bill?.Session?.PlannedDurationMin
            });
        }

        foreach (var payment in walletPayments)
        {
            dto.TotalWalletDeductions += payment.WalletAmount;
            dto.Transactions.Add(new WalletTransactionSummaryDto
            {
                Id = payment.Id,
                Timestamp = payment.CreatedAt,
                Description = $"Bill Payment via Wallet #{payment.Bill?.BillNumber} " +
                              $"({payment.Bill?.CustomerName ?? payment.Bill?.Member?.Username ?? "Walk-in"})",
                Amount = payment.WalletAmount,
                Action = "Deduction",
                PcName = payment.Bill?.Pc?.PcName ?? payment.Bill?.Pc?.PcNumber,
                DurationMinutes = payment.Bill?.Session?.ActualDurationMin ?? payment.Bill?.Session?.PlannedDurationMin
            });
        }

        dto.Transactions = dto.Transactions.OrderByDescending(t => t.Timestamp).ToList();

        return dto;
    }
}
