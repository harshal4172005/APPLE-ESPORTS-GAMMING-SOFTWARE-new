using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Cash;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class CashDeskService : ICashDeskService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;

    public CashDeskService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
    }

    public async Task StartVerificationAsync(Guid branchId, Guid operatorId, Guid shiftId)
    {
        var register = await _unitOfWork.Repository<CashRegister>().Query()
            .FirstOrDefaultAsync(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status == CashRegisterStatus.Open)
            ?? throw new NotFoundException("No open cash register found to verify.");

        register.Status = CashRegisterStatus.Verifying;
        _unitOfWork.Repository<CashRegister>().Update(register);
        
        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = "cash_desk_verification_started",
            BranchId = branchId,
            TargetType = "cash_register",
            TargetId = register.Id,
            Details = new { ExpectedDrawerCash = register.ExpectedDrawerCash }
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastCashRegisterUpdateAsync(branchId, register.Id);
    }

    public async Task<DenominationCountDto> SubmitDenominationsAsync(Guid branchId, Guid operatorId, Guid shiftId, SubmitDenominationDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var register = await _unitOfWork.Repository<CashRegister>().Query()
                .FirstOrDefaultAsync(r => r.BranchId == branchId && r.ShiftId == shiftId && r.Status == CashRegisterStatus.Verifying)
                ?? throw new NotFoundException("No verifying cash register found. Must start verification first.");

            decimal countedTotal = 
                (dto.Notes2000 * 2000) + 
                (dto.Notes500 * 500) + 
                (dto.Notes200 * 200) + 
                (dto.Notes100 * 100) + 
                (dto.Notes50 * 50) + 
                (dto.Notes20 * 20) + 
                (dto.Notes10 * 10) + 
                (dto.Coins5 * 5) + 
                (dto.Coins2 * 2) + 
                (dto.Coins1 * 1);

            var difference = countedTotal - register.ExpectedDrawerCash;
            var isVerified = difference == 0;

            if (!isVerified && string.IsNullOrEmpty(dto.MismatchReason))
                throw new AppException("Mismatch reason is required when drawer cash does not match expected total.");

            var countRecord = new DenominationCount
            {
                CashRegisterId = register.Id,
                ShiftId = shiftId,
                BranchId = branchId,
                OperatorId = operatorId,
                Notes2000 = dto.Notes2000,
                Notes500 = dto.Notes500,
                Notes200 = dto.Notes200,
                Notes100 = dto.Notes100,
                Notes50 = dto.Notes50,
                Notes20 = dto.Notes20,
                Notes10 = dto.Notes10,
                Coins5 = dto.Coins5,
                Coins2 = dto.Coins2,
                Coins1 = dto.Coins1,
                CountedTotal = countedTotal,
                ExpectedTotal = register.ExpectedDrawerCash,
                Difference = difference,
                IsVerified = isVerified,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<DenominationCount>().AddAsync(countRecord);

            // Update Register Status
            register.PhysicalCashCounted = countedTotal;
            register.CashDifference = difference;
            register.MismatchReason = dto.MismatchReason;
            register.Status = CashRegisterStatus.Verified;
            register.VerifiedAt = DateTimeOffset.UtcNow;
            
            _unitOfWork.Repository<CashRegister>().Update(register);

            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = "cash_desk_verified",
                BranchId = branchId,
                TargetType = "cash_register",
                TargetId = register.Id,
                Details = new { Expected = register.ExpectedDrawerCash, Actual = countedTotal, Difference = difference }
            });

            await _unitOfWork.CommitTransactionAsync();
            await _hubNotification.BroadcastCashRegisterUpdateAsync(branchId, register.Id);

            return MapToDto(countRecord);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task CloseRegisterAsync(Guid branchId, Guid operatorId, Guid shiftId, Guid cashRegisterId)
    {
        var register = await _unitOfWork.Repository<CashRegister>().Query()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.BranchId == branchId && r.ShiftId == shiftId)
            ?? throw new NotFoundException("Cash register not found.");

        if (register.Status != CashRegisterStatus.Verified)
            throw new AppException("Cash register must be verified before it can be closed.");

        register.Status = CashRegisterStatus.Closed;
        register.ClosedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<CashRegister>().Update(register);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = "cash_register_closed",
            BranchId = branchId,
            TargetType = "cash_register",
            TargetId = register.Id,
            Details = null
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastCashRegisterUpdateAsync(branchId, register.Id);
    }

    private static DenominationCountDto MapToDto(DenominationCount d)
    {
        return new DenominationCountDto
        {
            Id = d.Id,
            CashRegisterId = d.CashRegisterId,
            Notes2000 = d.Notes2000,
            Notes500 = d.Notes500,
            Notes200 = d.Notes200,
            Notes100 = d.Notes100,
            Notes50 = d.Notes50,
            Notes20 = d.Notes20,
            Notes10 = d.Notes10,
            Coins5 = d.Coins5,
            Coins2 = d.Coins2,
            Coins1 = d.Coins1,
            CountedTotal = d.CountedTotal,
            ExpectedTotal = d.ExpectedTotal,
            Difference = d.Difference,
            IsVerified = d.IsVerified,
            CreatedAt = d.CreatedAt
        };
    }
}
