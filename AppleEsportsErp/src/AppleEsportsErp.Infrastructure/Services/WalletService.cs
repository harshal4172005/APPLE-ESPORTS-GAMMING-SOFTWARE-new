using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Wallets;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public WalletService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<WalletTransactionDto> TopUpWalletAsync(Guid branchId, Guid operatorId, Guid shiftId, Guid memberId, TopUpWalletDto dto)
    {
        if (dto.Amount <= 0)
            throw new AppException("Top-up amount must be greater than zero.");

        var member = await _unitOfWork.Repository<Member>().GetByIdAsync(memberId)
            ?? throw new NotFoundException("Member not found.");

        var isGaming = dto.TargetWallet == WalletType.Gaming;
        var balanceBefore = isGaming ? member.GamingBalance : member.FoodBalance;
        
        if (isGaming)
            member.GamingBalance += dto.Amount;
        else
            member.FoodBalance += dto.Amount;
            
        var balanceAfter = isGaming ? member.GamingBalance : member.FoodBalance;

        _unitOfWork.Repository<Member>().Update(member);

        var walletTx = new WalletTransaction
        {
            MemberId = memberId,
            BranchId = branchId,
            OperatorId = operatorId,
            Action = WalletAction.Recharge,
            TargetWallet = dto.TargetWallet,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            PaymentType = dto.PaymentType,
            CashAmount = dto.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase) ? dto.Amount : 0,
            OnlineAmount = dto.PaymentType.Equals("Online", StringComparison.OrdinalIgnoreCase) ? dto.Amount : 0,
            Reason = dto.Reason ?? "Manual Top-Up",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<WalletTransaction>().AddAsync(walletTx);

        // If Cash, we must also update the Cash Register (SOP §10.3)
        if (walletTx.CashAmount > 0)
        {
            var activeRegister = await _unitOfWork.Repository<CashRegister>().Query()
                .FirstOrDefaultAsync(cr => cr.BranchId == branchId && cr.ShiftId == shiftId && cr.Status == CashRegisterStatus.Open);
                
            if (activeRegister == null)
                throw new AppException("Cannot process cash top-up: No active cash register found for this shift.");

            activeRegister.ExpectedDrawerCash += walletTx.CashAmount;
            _unitOfWork.Repository<CashRegister>().Update(activeRegister);

            var cashTx = new CashTransaction
            {
                CashRegisterId = activeRegister.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                TransactionType = "wallet_recharge",
                CashAmount = walletTx.CashAmount,
                GamingAmount = 0,
                FoodAmount = 0,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<CashTransaction>().AddAsync(cashTx);
        }

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = AuditActions.WalletRecharge,
            BranchId = branchId,
            TargetType = "wallet",
            TargetId = member.Id,
            Details = new { Amount = dto.Amount, PaymentType = dto.PaymentType }
        });

        await _unitOfWork.CommitTransactionAsync();

        return MapToDto(walletTx);
    }

    public async Task<WalletTransactionDto> DeductWalletAsync(Guid branchId, Guid operatorId, Guid memberId, DeductWalletDto dto)
    {
        if (dto.Amount <= 0)
            throw new AppException("Deduction amount must be greater than zero.");

        var member = await _unitOfWork.Repository<Member>().GetByIdAsync(memberId)
            ?? throw new NotFoundException("Member not found.");

        var isGaming = dto.TargetWallet == WalletType.Gaming;
        var balanceBefore = isGaming ? member.GamingBalance : member.FoodBalance;

        if (balanceBefore < dto.Amount)
            throw new AppException($"Insufficient {dto.TargetWallet} wallet balance. Current: {balanceBefore}, Required: {dto.Amount}");

        if (isGaming)
            member.GamingBalance -= dto.Amount;
        else
            member.FoodBalance -= dto.Amount;

        var balanceAfter = isGaming ? member.GamingBalance : member.FoodBalance;

        _unitOfWork.Repository<Member>().Update(member);

        var walletTx = new WalletTransaction
        {
            MemberId = memberId,
            BranchId = branchId,
            OperatorId = operatorId,
            Action = WalletAction.Correction,
            TargetWallet = dto.TargetWallet,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            PaymentType = "Wallet",
            CashAmount = 0,
            OnlineAmount = 0,
            BillId = dto.BillId,
            Reason = dto.Reason,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<WalletTransaction>().AddAsync(walletTx);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = AuditActions.WalletDeduction,
            BranchId = branchId,
            TargetType = "wallet",
            TargetId = member.Id,
            Details = new { Amount = dto.Amount, Reason = dto.Reason }
        });

        await _unitOfWork.CommitTransactionAsync();

        return MapToDto(walletTx);
    }

    public async Task<PaginatedResult<WalletTransactionDto>> GetWalletHistoryAsync(Guid memberId, int page = 1, int pageSize = 50)
    {
        var query = _unitOfWork.Repository<WalletTransaction>().Query()
            .Where(w => w.MemberId == memberId)
            .OrderByDescending(w => w.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return new PaginatedResult<WalletTransactionDto>(dtos, total, page, pageSize);
    }

    private static WalletTransactionDto MapToDto(WalletTransaction t)
    {
        return new WalletTransactionDto
        {
            Id = t.Id,
            MemberId = t.MemberId,
            Action = t.Action,
            TargetWallet = t.TargetWallet,
            Amount = t.Amount,
            BalanceBefore = t.BalanceBefore,
            BalanceAfter = t.BalanceAfter,
            PaymentType = t.PaymentType,
            Reason = t.Reason,
            CreatedAt = t.CreatedAt
        };
    }
}
