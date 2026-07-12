using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;
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

    public async Task<OnlineDeskSummaryDto> GetActiveOnlineDeskAsync(Guid branchId, Guid shiftId)
    {
        var shift = await _unitOfWork.Repository<Shift>().Query()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.BranchId == branchId);

        if (shift == null)
            throw new Exception("Shift not found.");

        var endTime = shift.LogoutTime ?? DateTimeOffset.UtcNow;

        // Find all bills for this shift
        var bills = await _unitOfWork.Repository<Bill>().Query()
            .Where(b => b.ShiftId == shiftId || (b.OperatorId == shift.OperatorId && b.CreatedAt >= shift.LoginTime && b.CreatedAt <= endTime))
            .Include(b => b.Payments)
            .Include(b => b.Member)
            .ToListAsync();

        var walletTxs = await _unitOfWork.Repository<WalletTransaction>().Query()
            .Where(w => w.BranchId == branchId && w.OperatorId == shift.OperatorId && w.CreatedAt >= shift.LoginTime && w.CreatedAt <= endTime)
            .Include(w => w.Member)
            .ToListAsync();

        var dto = new OnlineDeskSummaryDto
        {
            ShiftId = shiftId
        };

        foreach (var bill in bills)
        {
            foreach (var payment in bill.Payments)
            {
                if (payment.OnlineAmount > 0)
                {
                    dto.TotalOnlineSales += payment.OnlineAmount;
                    dto.Transactions.Add(new OnlineTransactionDto
                    {
                        Id = payment.Id,
                        Timestamp = payment.CreatedAt,
                        Description = $"Bill Payment #{bill.BillNumber} ({bill.CustomerName ?? bill.Member?.Username ?? "Walk-in"})",
                        Amount = payment.OnlineAmount,
                        PaymentMethod = "Online"
                    });
                }
            }
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

    public async Task<WalletDeskSummaryDto> GetActiveWalletDeskAsync(Guid branchId, Guid shiftId)
    {
        var shift = await _unitOfWork.Repository<Shift>().Query()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.BranchId == branchId);

        if (shift == null)
            throw new Exception("Shift not found.");

        var endTime = shift.LogoutTime ?? DateTimeOffset.UtcNow;

        var walletTxs = await _unitOfWork.Repository<WalletTransaction>().Query()
            .Where(w => w.BranchId == branchId && w.OperatorId == shift.OperatorId && w.CreatedAt >= shift.LoginTime && w.CreatedAt <= endTime)
            .Include(w => w.Member)
            .ToListAsync();

        var bills = await _unitOfWork.Repository<Bill>().Query()
            .Where(b => b.ShiftId == shiftId || (b.OperatorId == shift.OperatorId && b.CreatedAt >= shift.LoginTime && b.CreatedAt <= endTime))
            .Include(b => b.Payments)
            .Include(b => b.Member)
            .ToListAsync();

        var dto = new WalletDeskSummaryDto
        {
            ShiftId = shiftId
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
                Action = tx.Action.ToString()
            });
        }

        foreach (var bill in bills)
        {
            foreach (var payment in bill.Payments)
            {
                if (payment.WalletAmount > 0)
                {
                    dto.TotalWalletDeductions += payment.WalletAmount;
                    dto.Transactions.Add(new WalletTransactionSummaryDto
                    {
                        Id = payment.Id,
                        Timestamp = payment.CreatedAt,
                        Description = $"Bill Payment via Wallet #{bill.BillNumber} ({bill.CustomerName ?? bill.Member?.Username ?? "Walk-in"})",
                        Amount = payment.WalletAmount,
                        Action = "Deduction"
                    });
                }
            }
        }

        dto.Transactions = dto.Transactions.OrderByDescending(t => t.Timestamp).ToList();

        return dto;
    }
}
