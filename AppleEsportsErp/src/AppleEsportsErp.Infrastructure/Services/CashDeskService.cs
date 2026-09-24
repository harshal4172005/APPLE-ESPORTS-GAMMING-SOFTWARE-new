using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Cash;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Services;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;

namespace AppleEsportsErp.Infrastructure.Services;

public class CashDeskService : ICashDeskService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;
    private readonly IAdminNotifier _notifier;

    public CashDeskService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification,
        IAdminNotifier notifier)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
        _notifier = notifier;
    }

    public async Task StartVerificationAsync(Guid branchId, Guid operatorId, Guid shiftId)
    {
        // THIS shift's own register, by ShiftId - not "any Open register for the branch". That
        // used to be enough on its own (a branch has one physical cash box), but it silently
        // breaks the moment more than one Open register exists at once - a double-click on this
        // very button, firing the request twice, is enough to cause exactly that: the first
        // click finds and locks the real register, and because the query never named which
        // register it wanted, the second click - now with only one Open register left, if a
        // stray duplicate exists - can lock a completely different one instead. Confirmed live:
        // two "Start Verification" calls two seconds apart locked two different registers, one
        // of them empty, and every later attempt to fix it kept finding and cancelling that
        // same empty one - the real register, holding the shift's actual sales, was never
        // touched again. Pinning to ShiftId means there is only ever one possible answer.
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
        // Same reasoning as StartVerificationAsync above: THIS shift's own register, by ShiftId,
        // not "any Verifying register for the branch" - the count being submitted must land on
        // the same register this shift actually locked, never on an unrelated one that happens
        // to also be Verifying.
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
            register.CountedByOperatorId = operatorId;
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

            // After the commit, and only when the money is actually wrong. The count is the one
            // moment a shortfall is visible while anyone still remembers the evening; left to the
            // end-of-day figures it becomes an unexplained number nobody can account for.
            if (!isVerified)
                await NotifyOwnerOfCashDifferenceAsync(branchId, operatorId, register, countedTotal, difference, dto.MismatchReason);

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
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.BranchId == branchId)
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

    /// <summary>
    /// Tells the owner the drawer did not match, with both figures and the operator's reason.
    ///
    /// Short by more than it should be, or over — both are sent. Extra cash in a till is not good
    /// news: it usually means a sale went unrecorded, which is the same hole in the takings seen
    /// from the other side.
    /// </summary>
    private async Task NotifyOwnerOfCashDifferenceAsync(
        Guid branchId, Guid operatorId, CashRegister register,
        decimal countedTotal, decimal difference, string? reason)
    {
        try
        {
            var branch = await _unitOfWork.Repository<Branch>().Query()
                .FirstOrDefaultAsync(b => b.Id == branchId);
            var op = await _unitOfWork.Repository<Operator>().Query()
                .FirstOrDefaultAsync(o => o.Id == operatorId);

            var isShort = difference < 0;
            var amount = Math.Abs(difference);

            var rows = new List<(string Label, string Value)>
            {
                ("Branch", branch?.Name ?? "Unknown branch"),
                ("Counted by", op?.FullName ?? op?.Username ?? "Unknown operator"),
                ("", ""),
                ("Should have been in the drawer", $"Rs {register.ExpectedDrawerCash:N2}"),
                ("Actually counted", $"Rs {countedTotal:N2}"),
                (isShort ? "Missing" : "Extra", $"Rs {amount:N2}"),
                ("", ""),
                ("Reason given", string.IsNullOrWhiteSpace(reason) ? "None given" : reason.Trim()),
                ("Counted at", IndiaTime.Now.ToString("dd MMM yyyy, h:mm tt")),
            };

            var body = AdminEmailTemplate.Compose(
                heading: isShort ? "Cash is missing from the drawer" : "There is extra cash in the drawer",
                accent: isShort ? AdminEmailTemplate.Red : AdminEmailTemplate.Amber,
                summary: isShort
                    ? "The cash counted at the end of this shift is less than the system expected. " +
                      "The operator's reason is below."
                    : "The cash counted at the end of this shift is more than the system expected. " +
                      "This usually means a sale was not put through the system.",
                rows: rows,
                headline: $"Rs {amount:N2} {(isShort ? "short" : "over")}",
                footnote: "The shift was allowed to finish - the shop is not held up by this. " +
                          "The figures above are recorded against that shift.");

            await _notifier.NotifyAsync(
                $"{(isShort ? "Cash short" : "Cash over")} by Rs {amount:N0} at {branch?.Name ?? "a branch"}",
                body);
        }
        catch (Exception ex)
        {
            // Never allowed to undo a completed count. The money has been counted and recorded;
            // failing the whole operation because the mail did would lose the count itself.
            System.Diagnostics.Debug.WriteLine($"Could not send the cash difference email: {ex.Message}");
        }
    }

    public async Task CancelVerificationAsync(Guid branchId, Guid operatorId, Guid shiftId, Guid cashRegisterId)
    {
        var register = await _unitOfWork.Repository<CashRegister>().Query()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.BranchId == branchId)
            ?? throw new NotFoundException("Cash register not found.");

        // Already unlocked — nothing to cancel, treat as a no-op success.
        if (register.Status == CashRegisterStatus.Open)
            return;

        if (register.Status != CashRegisterStatus.Verifying)
            throw new AppException("Only a register that is currently locked for verification can be cancelled.");

        register.Status = CashRegisterStatus.Open;
        _unitOfWork.Repository<CashRegister>().Update(register);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = "cash_register_verification_cancelled",
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
