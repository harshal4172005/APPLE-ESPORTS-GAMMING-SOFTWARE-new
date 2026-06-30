using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Cash;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class CashRegisterService : ICashRegisterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;

    public CashRegisterService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
    }

    public async Task<CashRegisterDto> GetActiveRegisterAsync(Guid branchId, Guid shiftId)
    {
        var register = await _unitOfWork.Repository<CashRegister>().Query()
            .Include(r => r.CashTransactions)
            .FirstOrDefaultAsync(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status != CashRegisterStatus.Closed)
            ?? throw new NotFoundException("No active cash register found for this shift.");

        return MapToDto(register);
    }

    public async Task<CashRegisterDto> OpenRegisterAsync(Guid branchId, Guid operatorId, Guid shiftId, OpenRegisterDto dto)
    {
        var existing = await _unitOfWork.Repository<CashRegister>().Query()
            .FirstOrDefaultAsync(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status == CashRegisterStatus.Open);
            
        if (existing != null)
            throw new AppException("Cash register is already open for this shift.");

        var register = new CashRegister
        {
            BranchId = branchId,
            OperatorId = operatorId,
            ShiftId = shiftId,
            OpeningBalance = dto.OpeningBalance,
            ExpectedDrawerCash = dto.OpeningBalance, // Only opening balance affects drawer cash initially
            TotalCashSales = 0,
            TotalSplitCash = 0,
            Status = CashRegisterStatus.Open,
            OpenedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<CashRegister>().AddAsync(register);
        
        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = "cash_register_open",
            BranchId = branchId,
            TargetType = "cash_register",
            TargetId = register.Id,
            Details = new { OpeningBalance = dto.OpeningBalance }
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastCashRegisterUpdateAsync(branchId, register.Id);

        return MapToDto(register);
    }

    public async Task<CashRegisterDto> AddTransactionAsync(Guid branchId, Guid operatorId, Guid shiftId, AddCashTransactionDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var register = await _unitOfWork.Repository<CashRegister>().Query()
                .Include(r => r.CashTransactions)
                .FirstOrDefaultAsync(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status == CashRegisterStatus.Open)
                ?? throw new NotFoundException("No active cash register found for this shift.");

            var tx = new CashTransaction
            {
                CashRegisterId = register.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                CashAmount = dto.Amount,
                GamingAmount = 0,
                FoodAmount = 0,
                TransactionType = dto.TransactionType,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<CashTransaction>().AddAsync(tx);

            // Inwards increase drawer cash, Withdrawals/Expenses decrease drawer cash
            register.ExpectedDrawerCash += dto.Amount;
            
            _unitOfWork.Repository<CashRegister>().Update(register);

            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = $"cash_transaction_{dto.TransactionType}",
                BranchId = branchId,
                TargetType = "cash_register",
                TargetId = register.Id,
                Details = new { Amount = dto.Amount, Reason = dto.Reason }
            });

            await _unitOfWork.CommitTransactionAsync();
            await _hubNotification.BroadcastCashRegisterUpdateAsync(branchId, register.Id);

            return MapToDto(register);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static CashRegisterDto MapToDto(CashRegister r)
    {
        return new CashRegisterDto
        {
            Id = r.Id,
            ShiftId = r.ShiftId,
            BranchId = r.BranchId,
            OperatorId = r.OperatorId,
            OpeningBalance = r.OpeningBalance,
            TotalCashSales = r.TotalCashSales,
            TotalSplitCash = r.TotalSplitCash,
            ExpectedDrawerCash = r.ExpectedDrawerCash,
            PhysicalCashCounted = r.PhysicalCashCounted,
            CashDifference = r.CashDifference,
            MismatchReason = r.MismatchReason,
            Status = r.Status,
            OpenedAt = r.OpenedAt,
            VerifiedAt = r.VerifiedAt,
            ClosedAt = r.ClosedAt,
            Transactions = r.CashTransactions?.Select(tx => new CashTransactionDto
            {
                Id = tx.Id,
                BillId = tx.BillId,
                PcNumber = tx.PcNumber,
                CashAmount = tx.CashAmount,
                GamingAmount = tx.GamingAmount,
                FoodAmount = tx.FoodAmount,
                TransactionType = tx.TransactionType,
                CreatedAt = tx.CreatedAt
            }).ToList() ?? new List<CashTransactionDto>()
        };
    }
}
