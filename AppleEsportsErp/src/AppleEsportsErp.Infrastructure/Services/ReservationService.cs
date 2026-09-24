using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Reservations;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Services;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Configuration;

namespace AppleEsportsErp.Infrastructure.Services;

public class ReservationService : IReservationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IHubNotificationService _hubNotification;
    private readonly IConfiguration _configuration;

    public ReservationService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IHubNotificationService hubNotification,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _hubNotification = hubNotification;
        _configuration = configuration;
    }

    /// <summary>
    /// A booking made here and nowhere else. Head Office holds a synced copy of every
    /// branch's reservations, and writing one into that copy is easy and looks correct
    /// immediately - the screen shows it booked - but the branch that actually has the PC
    /// and the counter was never told. No machine is held, nothing appears on the operator's
    /// own list, and a customer who was promised a booking finds nobody expecting them.
    /// Same reason SessionService and BillingService refuse to write directly at Head
    /// Office: see ReservationsController for the branch-command instruction sent instead.
    /// </summary>
    private void RefuseIfHeadOffice(string what)
    {
        if (!_configuration.IsHeadOffice()) return;

        throw new AppException(
            $"This reservation has to be {what} by the branch itself, not written here at Head " +
            "Office - a reservation written here is invisible to the counter, so nobody there " +
            "would know to expect the customer. Send it as an instruction instead and the " +
            "branch will carry it out within a few seconds and report back.",
            System.Net.HttpStatusCode.BadRequest,
            "BRANCH_ONLY_OPERATION");
    }

    public async Task<PaginatedResult<ReservationDto>> GetActiveReservationsAsync(Guid branchId, int page = 1, int pageSize = 50)
    {
        await ExpirePastReservationsAsync(branchId); // Passive expiration check

        var query = _unitOfWork.Repository<Reservation>().Query()
            .Include(r => r.Pc)
            .Where(r => r.BranchId == branchId && r.State == ReservationState.Pending)
            .OrderBy(r => r.ReservationTime);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return new PaginatedResult<ReservationDto>(dtos, total, page, pageSize);
    }

    /// <summary>
    /// Every reservation in a date range, any state - unlike GetActiveReservationsAsync, which
    /// deliberately shows only Pending because that is the operator's own to-do list. A look
    /// back at a past day wants everything that was booked that day, arrived or not.
    /// </summary>
    public async Task<List<ReservationDto>> GetReservationHistoryAsync(Guid branchId, DateOnly fromDate, DateOnly toDate)
    {
        var (dayStart, _) = IndiaTime.BusinessDayRange(fromDate);
        var (_, dayEnd) = IndiaTime.BusinessDayRange(toDate);

        var reservations = await _unitOfWork.Repository<Reservation>().Query()
            .Include(r => r.Pc)
            .Where(r => r.BranchId == branchId && r.ReservationTime >= dayStart && r.ReservationTime < dayEnd)
            .OrderByDescending(r => r.ReservationTime)
            .ToListAsync();

        return reservations.Select(MapToDto).ToList();
    }

    public async Task<ReservationDto> GetReservationAsync(Guid id)
    {
        var reservation = await _unitOfWork.Repository<Reservation>().Query()
            .Include(r => r.Pc)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException($"Reservation {id} not found.");
            
        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CreateReservationAsync(Guid branchId, Guid operatorId, CreateReservationDto dto)
    {
        RefuseIfHeadOffice("booked");
        var pc = await _unitOfWork.Repository<Pc>().GetByIdAsync(dto.PcId)
            ?? throw new NotFoundException("PC not found.");

        if (pc.BranchId != branchId)
            throw new BranchIsolationException("PC belongs to another branch.");

        // SOP §8: Prevent overlapping reservations within the same time window
        var overlapping = await _unitOfWork.Repository<Reservation>().Query()
            .Where(r => r.PcId == dto.PcId && r.State == ReservationState.Pending)
            .Where(r => r.ReservationTime < dto.ReservationTime.AddMinutes(dto.DurationMin ?? 60)
                     && r.ReservationTime.AddMinutes(r.DurationMin ?? 60) > dto.ReservationTime)
            .AnyAsync();

        if (overlapping)
            throw new AppException("PC is already reserved for this time slot.");

        var gracePeriod = (dto.GracePeriodMin.HasValue && dto.GracePeriodMin.Value > 0) ? dto.GracePeriodMin.Value : 15;

        // The deposit total still covers both portions - it is credited against the customer's
        // final bill regardless of how it was paid (see the bill-completion path below). Only
        // the cash portion is ever allowed to touch the drawer.
        var advanceDeposit = dto.AdvanceDepositCash + dto.AdvanceDepositOnline;

        var reservation = new Reservation
        {
            PcId = dto.PcId,
            BranchId = branchId,
            OperatorId = operatorId,
            CustomerName = dto.CustomerName,
            MemberId = dto.MemberId,
            ReservationTime = dto.ReservationTime,
            DurationMin = dto.DurationMin,
            GracePeriodMin = gracePeriod,
            AdvanceDeposit = advanceDeposit,
            State = ReservationState.Pending,
            Notes = dto.Notes
        };

        // Reflect the booking on the PC immediately, regardless of how far out it starts —
        // an operator/customer glancing at the floor grid should see it's spoken for right away.
        if (pc.State == PcState.Idle)
        {
            pc.State = PcState.Reserved;
            pc.CurrentReservation = reservation;
            _unitOfWork.Repository<Pc>().Update(pc);
        }

        // Fetch operator's active shift and record the CASH portion of the advance deposit.
        // The online portion never touched the drawer, so it never adds to ExpectedDrawerCash -
        // it used to be added unconditionally here, which permanently inflated what the drawer
        // was expected to hold by however much of the deposit was actually paid online.
        var activeShift = await _unitOfWork.Repository<Shift>().Query()
            .FirstOrDefaultAsync(s => s.OperatorId == operatorId && s.BranchId == branchId && s.Status == ShiftStatus.Active);
        var shiftId = activeShift?.Id;

        if (dto.AdvanceDepositCash > 0 && shiftId.HasValue)
        {
            var activeRegister = await _unitOfWork.Repository<CashRegister>().Query()
                .FirstOrDefaultAsync(cr => cr.BranchId == branchId && cr.ShiftId == shiftId.Value && cr.Status == CashRegisterStatus.Open);

            if (activeRegister != null)
            {
                activeRegister.ExpectedDrawerCash += dto.AdvanceDepositCash;
                activeRegister.TotalCashSales += dto.AdvanceDepositCash;
                _unitOfWork.Repository<CashRegister>().Update(activeRegister);

                var cashTx = new CashTransaction
                {
                    CashRegisterId = activeRegister.Id,
                    BranchId = branchId,
                    OperatorId = operatorId,
                    TransactionType = "reservation_deposit",
                    CashAmount = dto.AdvanceDepositCash,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _unitOfWork.Repository<CashTransaction>().AddAsync(cashTx);
            }
        }

        await _unitOfWork.Repository<Reservation>().AddAsync(reservation);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = AuditActions.ReservationCreate,
            BranchId = branchId,
            TargetType = "reservation",
            TargetId = reservation.Id,
            Details = new
            {
                CustomerName = dto.CustomerName,
                PcNumber = pc.PcNumber,
                ReservationTime = dto.ReservationTime,
                AdvanceDepositCash = dto.AdvanceDepositCash,
                AdvanceDepositOnline = dto.AdvanceDepositOnline
            }
        });

        await _unitOfWork.CommitTransactionAsync();

        // Broadcast notifications
        await _hubNotification.BroadcastReservationUpdateAsync(branchId, reservation.Id);
        await _hubNotification.BroadcastPcStatusChangeAsync(branchId, pc.Id);

        reservation.Pc = pc;
        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CancelReservationAsync(Guid branchId, Guid operatorId, Guid id, CancelReservationDto dto)
    {
        RefuseIfHeadOffice("cancelled");
        var reservation = await _unitOfWork.Repository<Reservation>().Query()
            .Include(r => r.Pc)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Reservation not found.");

        if (reservation.BranchId != branchId)
            throw new BranchIsolationException("Reservation belongs to another branch.");

        if (reservation.State != ReservationState.Pending)
            throw new AppException("Only pending reservations can be cancelled.");

        reservation.State = ReservationState.Cancelled;
        reservation.CancelledAt = DateTimeOffset.UtcNow;
        reservation.Notes = (reservation.Notes + $" [Cancelled: {dto.Reason}]").Trim();

        _unitOfWork.Repository<Reservation>().Update(reservation);

        // Free up the PC if it was marked as reserved for this
        var pc = await _unitOfWork.Repository<Pc>().GetByIdAsync(reservation.PcId);
        if (pc != null && pc.State == PcState.Reserved)
        {
            // Check if there are other pending reservations right now
            var hasOther = await _unitOfWork.Repository<Reservation>().Query()
                .AnyAsync(r => r.PcId == pc.Id && r.State == ReservationState.Pending && r.Id != reservation.Id 
                          && r.ReservationTime <= DateTimeOffset.UtcNow.AddMinutes(15));
                          
            if (!hasOther)
            {
                pc.State = PcState.Idle;
                _unitOfWork.Repository<Pc>().Update(pc);
                await _hubNotification.BroadcastPcStatusChangeAsync(branchId, pc.Id);
            }
        }

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = AuditActions.ReservationCancel,
            BranchId = branchId,
            TargetType = "reservation",
            TargetId = reservation.Id,
            Details = new { Reason = dto.Reason }
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastReservationUpdateAsync(branchId, reservation.Id);

        return MapToDto(reservation);
    }

    /// <summary>
    /// A plain reminder flag, hand-set at the counter - not routed through Head Office when set
    /// there, unlike every other action here. There is nothing for a branch to carry out: no PC
    /// to free, no session to start, nothing with a money or audit consequence. Reservation is
    /// already a synced entity (SyncCapture.Watched), so this still reaches Head Office's own
    /// copy the ordinary way when set at a branch - it just never needs to travel the other
    /// direction, because nothing there depends on it.
    /// </summary>
    public async Task<ReservationDto> SetArrivedAsync(Guid branchId, Guid id, bool arrived)
    {
        var reservation = await _unitOfWork.Repository<Reservation>().Query()
            .Include(r => r.Pc)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Reservation not found.");

        if (reservation.BranchId != branchId)
            throw new BranchIsolationException("Reservation belongs to another branch.");

        reservation.Arrived = arrived;
        _unitOfWork.Repository<Reservation>().Update(reservation);
        await _unitOfWork.CommitTransactionAsync();

        return MapToDto(reservation);
    }

    /// <summary>Permanently removes a reservation - the "Remove" button, distinct from Cancel:
    /// no reason kept, no Cancelled record left behind, just gone. Frees the PC exactly like
    /// Cancel does if it was still holding it for this booking.</summary>
    public async Task DeleteReservationAsync(Guid branchId, Guid operatorId, Guid id)
    {
        var reservation = await _unitOfWork.Repository<Reservation>().Query()
            .Include(r => r.Pc)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Reservation not found.");

        if (reservation.BranchId != branchId)
            throw new BranchIsolationException("Reservation belongs to another branch.");

        var pc = await _unitOfWork.Repository<Pc>().GetByIdAsync(reservation.PcId);
        if (pc != null)
        {
            if (pc.CurrentReservationId == reservation.Id)
                pc.CurrentReservationId = null;

            if (pc.State == PcState.Reserved)
            {
                var hasOther = await _unitOfWork.Repository<Reservation>().Query()
                    .AnyAsync(r => r.PcId == pc.Id && r.State == ReservationState.Pending && r.Id != reservation.Id
                              && r.ReservationTime <= DateTimeOffset.UtcNow.AddMinutes(15));

                if (!hasOther)
                    pc.State = PcState.Idle;
            }

            _unitOfWork.Repository<Pc>().Update(pc);
            await _hubNotification.BroadcastPcStatusChangeAsync(branchId, pc.Id);
        }

        _unitOfWork.Repository<Reservation>().Remove(reservation);

        await _auditService.LogAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "Operator",
            UserName = "System",
            Action = AuditActions.ReservationDelete,
            BranchId = branchId,
            TargetType = "reservation",
            TargetId = reservation.Id,
            Details = new { reservation.CustomerName, reservation.PcId }
        });

        await _unitOfWork.CommitTransactionAsync();
        await _hubNotification.BroadcastReservationUpdateAsync(branchId, reservation.Id);
    }

    public async Task<ReservationDto> StartReservedSessionAsync(Guid branchId, Guid operatorId, Guid id)
    {
        RefuseIfHeadOffice("started");
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var reservation = await _unitOfWork.Repository<Reservation>().Query()
                .Include(r => r.Pc)
                .FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new NotFoundException("Reservation not found.");

            if (reservation.BranchId != branchId)
                throw new BranchIsolationException("Reservation belongs to another branch.");

            if (reservation.State != ReservationState.Pending)
                throw new AppException("Only pending reservations can be started.");

            var pc = await _unitOfWork.Repository<Pc>().Query()
                .Include(p => p.PricingProfile)
                .FirstOrDefaultAsync(p => p.Id == reservation.PcId)
                ?? throw new NotFoundException("PC not found.");

            if (pc.State != PcState.Idle && pc.State != PcState.Reserved)
                throw new AppException($"Cannot start session. PC is currently {pc.State}");

            var now = DateTimeOffset.UtcNow;
            
            // 1. Transition Reservation state to Completed since the session is taking over
            reservation.State = ReservationState.Completed;
            reservation.StartedAt = now;
            _unitOfWork.Repository<Reservation>().Update(reservation);

            // 2. Fetch operator's active shift
            var activeShift = await _unitOfWork.Repository<Shift>().Query()
                .FirstOrDefaultAsync(s => s.OperatorId == operatorId && s.BranchId == branchId && s.Status == ShiftStatus.Active);
            var shiftId = activeShift?.Id;

            // 3. Compute expected amount based on PC rate and duration
            var ratePerHour = pc.PricingProfile?.BaseHourlyRate ?? (reservation.MemberId.HasValue ? 80m : 100m);
            var durationMin = reservation.DurationMin ?? 60;
            var expectedAmount = (durationMin / 60m) * ratePerHour;

            // 4. Create Session
            var session = new Session
            {
                Id = Guid.NewGuid(),
                PcId = pc.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                ShiftId = shiftId == Guid.Empty ? null : shiftId,
                CustomerName = reservation.CustomerName,
                MemberId = reservation.MemberId,
                StartTime = now,
                EndTime = now.AddMinutes(durationMin),
                PlannedDurationMin = durationMin,
                TotalAmount = expectedAmount,
                GamingAmount = expectedAmount,
                GamingType = "Reservation - " + durationMin + "m",
                State = SessionState.Active,
                Notes = reservation.Notes,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _unitOfWork.Repository<Session>().AddAsync(session);

            // 5. Create Bill (subtracting AdvanceDeposit from TotalAmount)
            var totalAmount = Math.Max(0m, expectedAmount - reservation.AdvanceDeposit);
            var bill = new Bill
            {
                Id = Guid.NewGuid(),
                BillNumber = $"BILL-{now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                SessionId = session.Id,
                PcId = pc.Id,
                BranchId = branchId,
                OperatorId = operatorId,
                ShiftId = shiftId == Guid.Empty ? null : shiftId,
                CustomerName = reservation.CustomerName,
                MemberId = reservation.MemberId,
                GamingAmount = expectedAmount,
                FoodAmount = 0,
                Subtotal = expectedAmount,
                TotalAmount = totalAmount,
                Status = BillStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };
            
            if (reservation.AdvanceDeposit > 0)
            {
                bill.DiscountReason = $"Advance deposit of ₹{reservation.AdvanceDeposit} applied";
            }
            
            await _unitOfWork.Repository<Bill>().AddAsync(bill);

            // 6. Update PC state to Active
            pc.State = PcState.Active;
            pc.CurrentSessionId = session.Id;
            pc.CurrentReservationId = reservation.Id; // keep it linked while active
            _unitOfWork.Repository<Pc>().Update(pc);

            // 7. Audit log
            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = AuditActions.SessionStart,
                BranchId = branchId,
                TargetType = "session",
                TargetId = session.Id,
                Details = new { PcNumber = pc.PcNumber, reservation.CustomerName, ReservationId = reservation.Id }
            });

            await _unitOfWork.CommitTransactionAsync();

            // Broadcast updates
            await _hubNotification.BroadcastReservationUpdateAsync(branchId, reservation.Id);
            await _hubNotification.BroadcastPcStatusChangeAsync(branchId, pc.Id);
            await _hubNotification.BroadcastSessionUpdateAsync(branchId, session.Id);
            await _hubNotification.BroadcastBillingUpdateAsync(branchId, bill.Id);

            return MapToDto(reservation);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ReservationDto> OverrideReservationAsync(Guid branchId, Guid operatorId, Guid id, OverrideReservationDto dto)
    {
        RefuseIfHeadOffice("overridden");
        var reservation = await _unitOfWork.Repository<Reservation>().Query()
            .Include(r => r.Pc)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Reservation not found.");

        if (reservation.BranchId != branchId)
            throw new BranchIsolationException("Reservation belongs to another branch.");

        if (reservation.State != ReservationState.Pending)
            throw new AppException("Only pending reservations can be overridden.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Transition state to Overridden
            reservation.State = ReservationState.Overridden;
            
            var adminUser = await _unitOfWork.Repository<User>().Query()
                .FirstOrDefaultAsync(u => u.Role == Roles.SuperAdmin);
            reservation.OverrideBy = adminUser?.Id;

            reservation.OverrideReason = dto.Reason;
            reservation.Notes = (reservation.Notes + $" [Overridden by Admin: {dto.Reason}]").Trim();
            _unitOfWork.Repository<Reservation>().Update(reservation);

            // Release PC to Idle
            var pc = await _unitOfWork.Repository<Pc>().Query().FirstOrDefaultAsync(p => p.Id == reservation.PcId);
            if (pc != null)
            {
                pc.State = PcState.Idle;
                pc.CurrentReservationId = null;
                _unitOfWork.Repository<Pc>().Update(pc);
                await _hubNotification.BroadcastPcStatusChangeAsync(branchId, pc.Id);
            }

            // Log Audit
            await _auditService.LogAsync(new AuditEntry
            {
                OperatorId = operatorId,
                UserRole = "Operator",
                UserName = "System",
                Action = AuditActions.ReservationOverride,
                BranchId = branchId,
                TargetType = "reservation",
                TargetId = reservation.Id,
                Details = new { Reason = dto.Reason }
            });

            await _unitOfWork.CommitTransactionAsync();
            await _hubNotification.BroadcastReservationUpdateAsync(branchId, reservation.Id);

            return MapToDto(reservation);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task ExpirePastReservationsAsync(Guid branchId)
    {
        // SOP §8: Grace period of 15 minutes before auto-cancelling
        var expiredThreshold = DateTimeOffset.UtcNow.AddMinutes(-15);
        
        var expiredReservations = await _unitOfWork.Repository<Reservation>().Query()
            .Where(r => r.BranchId == branchId && r.State == ReservationState.Pending && r.ReservationTime < expiredThreshold)
            .ToListAsync();

        if (!expiredReservations.Any())
            return;

        foreach (var res in expiredReservations)
        {
            res.State = ReservationState.Expired;
            res.ExpiredAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<Reservation>().Update(res);

            // Free the PC if reserved
            var pc = await _unitOfWork.Repository<Pc>().GetByIdAsync(res.PcId);
            if (pc != null && pc.State == PcState.Reserved)
            {
                pc.State = PcState.Idle;
                _unitOfWork.Repository<Pc>().Update(pc);
                await _hubNotification.BroadcastPcStatusChangeAsync(branchId, pc.Id);
            }

            await _hubNotification.BroadcastReservationUpdateAsync(branchId, res.Id);
        }

        await _unitOfWork.CommitTransactionAsync();
    }

    private static ReservationDto MapToDto(Reservation r)
    {
        return new ReservationDto
        {
            Id = r.Id,
            PcId = r.PcId,
            CustomerName = r.CustomerName,
            MemberId = r.MemberId,
            ReservationTime = r.ReservationTime,
            DurationMin = r.DurationMin,
            State = r.State,
            Notes = r.Notes,
            AdvanceDeposit = r.AdvanceDeposit,
            GracePeriodMin = r.GracePeriodMin,
            PcName = r.Pc?.PcNumber,
            Arrived = r.Arrived
        };
    }
}
