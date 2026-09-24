using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Reservations;
using AppleEsportsErp.Application.Interfaces;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
[BranchIsolation]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly AppleEsportsErp.Infrastructure.Data.AppDbContext _db;
    private readonly AppleEsportsErp.Api.Services.IRemoteBranchControl _remote;

    public ReservationsController(
        IReservationService reservationService,
        AppleEsportsErp.Infrastructure.Data.AppDbContext db,
        AppleEsportsErp.Api.Services.IRemoteBranchControl remote)
    {
        _reservationService = reservationService;
        _db = db;
        _remote = remote;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>
    /// Same reasoning as SessionsController.SendToBranchAsync / BillingController's own copy:
    /// a reservation booked here only ever existed here. Reservation was never synced upward
    /// either (fixed separately - see SyncCapture.Watched), so a customer told "you're booked"
    /// by Head Office had nothing at the branch backing that promise: no PC held, no entry on
    /// the counter's own reservation list, nothing for the operator to honour or even know
    /// about when the customer walked in.
    /// </summary>
    private async Task<IActionResult> SendToBranchAsync(
        Guid branchId, string commandType, object payload, CancellationToken ct)
    {
        var receipt = await _remote.SendAsync(branchId, commandType, payload, CurrentUserId(), ct);

        return Accepted(ApiResponse<object>.Ok(new
        {
            queued = true,
            commandId = receipt.CommandId,
            branchIsReporting = receipt.BranchIsReporting,
            message = receipt.Message,
        }));
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveReservations([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _reservationService.GetActiveReservationsAsync(GetBranchId(), page, pageSize);
        return Ok(ApiResponse<PaginatedResult<ReservationDto>>.Ok(result));
    }

    /// <summary>Every reservation (any state) in a date range - the History view, separate from
    /// the live "pending only" to-do list GetActiveReservations feeds.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetReservationHistory([FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate)
    {
        var result = await _reservationService.GetReservationHistoryAsync(GetBranchId(), fromDate, toDate);
        return Ok(ApiResponse<List<ReservationDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationDto dto, CancellationToken ct)
    {
        try
        {
            if (_remote.MustTravel)
            {
                // Taken from the PC named in the request, not from a header - the PC belongs
                // to exactly one shop, so this cannot book a machine at a branch that has no
                // such PC because a header was stale or absent.
                var branchId = await _db.Pcs.AsNoTracking()
                    .Where(p => p.Id == dto.PcId).Select(p => p.BranchId).FirstOrDefaultAsync(ct);

                if (branchId == Guid.Empty)
                    return NotFound(ApiResponse<object>.Fail("Head Office has no such PC.", "PC_NOT_FOUND"));

                return await SendToBranchAsync(branchId, AppleEsportsErp.Api.Services.BranchCommands.CreateReservation, new
                {
                    pcId = dto.PcId,
                    customerName = dto.CustomerName,
                    memberId = dto.MemberId,
                    reservationTime = dto.ReservationTime,
                    durationMin = dto.DurationMin,
                    notes = dto.Notes,
                    advanceDepositCash = dto.AdvanceDepositCash,
                    advanceDepositOnline = dto.AdvanceDepositOnline,
                    gracePeriodMin = dto.GracePeriodMin,
                }, ct);
            }

            var result = await _reservationService.CreateReservationAsync(GetBranchId(), (await this.GetOperatorIdAsync()), dto);
            return Ok(ApiResponse<ReservationDto>.Ok(result));
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = $"Reservation failed: {ex.Message}" });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelReservation(Guid id, [FromBody] CancelReservationDto dto, CancellationToken ct)
    {
        if (_remote.MustTravel)
        {
            var branchId = await _db.Set<AppleEsportsErp.Domain.Entities.Reservation>().AsNoTracking()
                .Where(r => r.Id == id).Select(r => r.BranchId).FirstOrDefaultAsync(ct);

            if (branchId == Guid.Empty)
                return NotFound(ApiResponse<object>.Fail("Head Office has no such reservation.", "RESERVATION_NOT_FOUND"));

            return await SendToBranchAsync(branchId, AppleEsportsErp.Api.Services.BranchCommands.CancelReservation, new
            {
                reservationId = id,
                reason = dto.Reason,
            }, ct);
        }

        var result = await _reservationService.CancelReservationAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, dto);
        return Ok(ApiResponse<ReservationDto>.Ok(result));
    }

    /// <summary>The "Remove" button - permanently deletes a reservation, no reason kept.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReservation(Guid id, CancellationToken ct)
    {
        if (_remote.MustTravel)
        {
            var branchId = await _db.Set<AppleEsportsErp.Domain.Entities.Reservation>().AsNoTracking()
                .Where(r => r.Id == id).Select(r => r.BranchId).FirstOrDefaultAsync(ct);

            if (branchId == Guid.Empty)
                return NotFound(ApiResponse<object>.Fail("Head Office has no such reservation.", "RESERVATION_NOT_FOUND"));

            return await SendToBranchAsync(branchId, AppleEsportsErp.Api.Services.BranchCommands.DeleteReservation, new
            {
                reservationId = id,
            }, ct);
        }

        await _reservationService.DeleteReservationAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id);
        return Ok(ApiResponse<object>.Ok(null));
    }

    /// <summary>A plain "the customer is here" reminder flag - see ReservationService.
    /// SetArrivedAsync. Never routed to a branch even when called from Head Office: nothing a
    /// branch needs to carry out follows from it.</summary>
    [HttpPut("{id}/arrived")]
    public async Task<IActionResult> SetArrived(Guid id, [FromBody] SetArrivedDto dto, CancellationToken ct)
    {
        var result = await _reservationService.SetArrivedAsync(GetBranchId(), id, dto.Arrived);
        return Ok(ApiResponse<ReservationDto>.Ok(result));
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartReservedSession(Guid id, CancellationToken ct)
    {
        if (_remote.MustTravel)
        {
            var branchId = await _db.Set<AppleEsportsErp.Domain.Entities.Reservation>().AsNoTracking()
                .Where(r => r.Id == id).Select(r => r.BranchId).FirstOrDefaultAsync(ct);

            if (branchId == Guid.Empty)
                return NotFound(ApiResponse<object>.Fail("Head Office has no such reservation.", "RESERVATION_NOT_FOUND"));

            return await SendToBranchAsync(branchId, AppleEsportsErp.Api.Services.BranchCommands.StartReservation, new
            {
                reservationId = id,
            }, ct);
        }

        var result = await _reservationService.StartReservedSessionAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id);
        return Ok(ApiResponse<ReservationDto>.Ok(result));
    }

    [HttpPost("{id}/override")]
    public async Task<IActionResult> OverrideReservation(Guid id, [FromBody] OverrideReservationDto dto, CancellationToken ct)
    {
        if (_remote.MustTravel)
        {
            var branchId = await _db.Set<AppleEsportsErp.Domain.Entities.Reservation>().AsNoTracking()
                .Where(r => r.Id == id).Select(r => r.BranchId).FirstOrDefaultAsync(ct);

            if (branchId == Guid.Empty)
                return NotFound(ApiResponse<object>.Fail("Head Office has no such reservation.", "RESERVATION_NOT_FOUND"));

            return await SendToBranchAsync(branchId, AppleEsportsErp.Api.Services.BranchCommands.OverrideReservation, new
            {
                reservationId = id,
                reason = dto.Reason,
            }, ct);
        }

        var result = await _reservationService.OverrideReservationAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, dto);
        return Ok(ApiResponse<ReservationDto>.Ok(result));
    }
}

