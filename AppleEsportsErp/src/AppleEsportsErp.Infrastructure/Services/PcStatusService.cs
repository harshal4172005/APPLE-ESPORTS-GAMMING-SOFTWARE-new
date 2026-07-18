using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppleEsportsErp.Application.DTOs.PcStatus;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Services;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Infrastructure.Services;

public class PcStatusService : IPcStatusService
{
    private readonly AppDbContext _db;
    private readonly IHubNotificationService _hubNotifier;
    private readonly ILogger<PcStatusService> _logger;

    public PcStatusService(AppDbContext db, IHubNotificationService hubNotifier, ILogger<PcStatusService> logger)
    {
        _db = db;
        _hubNotifier = hubNotifier;
        _logger = logger;
    }

    public async Task<IEnumerable<PcStatusDto>> GetBranchPcStatusesAsync(Guid branchId)
    {
        var pcs = await _db.Pcs
            .AsNoTracking()
            .Include(p => p.PricingProfile)
            .Where(p => p.BranchId == branchId && !p.IsDeleted)
            .OrderBy(p => p.PcNumber)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;

        // Fetch active sessions for these PCs
        var activeSessions = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Bills)
            .Where(s => s.BranchId == branchId && (s.State == SessionState.Active || s.State == SessionState.AwaitingBilling))
            .ToDictionaryAsync(s => s.PcId, s => s);

        // Fetch the most recent completed session for each PC (for quick restart)
        var recentCompletedSessions = await _db.Sessions
            .AsNoTracking()
            .Where(s => s.BranchId == branchId && s.State == SessionState.Completed)
            .GroupBy(s => s.PcId)
            .Select(g => g.OrderByDescending(s => s.EndTime).FirstOrDefault())
            .ToDictionaryAsync(s => s!.PcId, s => s);

        // Fetch pending reservations for these PCs (current + upcoming)
        var upcomingReservations = await _db.Reservations
            .AsNoTracking()
            .Where(r => r.BranchId == branchId && r.State == ReservationState.Pending)
            .OrderBy(r => r.ReservationTime)
            .ToListAsync();

        var reservationDict = upcomingReservations
            .GroupBy(r => r.PcId)
            .ToDictionary(g => g.Key, g => g.First()); // Get the next immediate reservation

        var result = new List<PcStatusDto>();

        foreach (var pc in pcs)
        {
            decimal calculatedRate = SessionPricingCalculator.DefaultRatePerHour;
            int bufferMinutes = SessionPricingCalculator.DefaultBufferMinutes;
            if (pc.PricingProfile != null)
            {
                calculatedRate = pc.PricingProfile.BaseHourlyRate;
                bufferMinutes = pc.PricingProfile.BufferMinutes;
            }

            var dto = new PcStatusDto
            {
                Id = pc.Id,
                Name = pc.PcNumber,
                IpAddress = pc.IpAddress ?? string.Empty,
                State = pc.State,
                BranchId = pc.BranchId,
                Zone = pc.Zone ?? "Standard",
                MonitorHz = pc.MonitorHz,
                RatePerHour = calculatedRate,
                BufferMinutes = bufferMinutes,
                IsAgentOnline = pc.IsAgentOnline,
                ConnectionMode = pc.ConnectionMode
            };

            Session? session = null;
            if (activeSessions.TryGetValue(pc.Id, out session))
            {
                dto.ActiveSessionId = session.Id;
                dto.CustomerName = session.CustomerName;
                dto.SessionStartTime = session.StartTime;
                dto.CustomerType = session.MemberId.HasValue ? "Member" : "Walk-in";
                var activeBill = session.Bills.FirstOrDefault();
                dto.ActiveBillId = activeBill?.Id;
                dto.FoodAmount = session.FoodAmount;

                if (session.State == SessionState.Active)
                {
                    // Still running — compute the live charge with the exact same formula
                    // StopSessionAsync will use, so this number never diverges from the real bill.
                    decimal elapsedMinutes = (decimal)(now - session.StartTime).TotalMinutes;
                    decimal liveGamingAmount = SessionPricingCalculator.CalculateGamingAmount(calculatedRate, bufferMinutes, elapsedMinutes);
                    dto.TotalAmount = liveGamingAmount + session.FoodAmount;
                }
                else
                {
                    // Already stopped (Awaiting Billing) — amount is final, just display it.
                    dto.TotalAmount = activeBill?.TotalAmount ?? session.TotalAmount;
                }

                if (session.EndTime.HasValue)
                    dto.SessionEndTime = session.EndTime;
            }
            else if (recentCompletedSessions.TryGetValue(pc.Id, out var lastSession) && lastSession != null)
            {
                // If there's no active session, provide the last customer details for a quick restart
                dto.LastCustomerName = lastSession.CustomerName;
                dto.LastMemberId = lastSession.MemberId;
            }

            if (reservationDict.TryGetValue(pc.Id, out var res))
            {
                dto.NextReservationId = res.Id;
                dto.NextReservationTime = res.ReservationTime;
                dto.CustomerName = dto.CustomerName ?? res.CustomerName;

                if (session != null)
                {
                    if (session.EndTime.HasValue)
                    {
                        if (session.EndTime.Value > res.ReservationTime)
                        {
                            dto.HasOverrunWarning = true;
                            dto.OverrunWarningMessage = $"Active session duration extends past reservation time ({res.ReservationTime.ToLocalTime():HH:mm}).";
                        }
                    }
                    else
                    {
                        if (res.ReservationTime <= now.AddMinutes(30))
                        {
                            dto.HasOverrunWarning = true;
                            dto.OverrunWarningMessage = $"Open-ended session might overlap with upcoming reservation starting at {res.ReservationTime.ToLocalTime():HH:mm}.";
                        }
                    }
                }
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<PcStatusDto> GetPcStatusAsync(Guid pcId)
    {
        var pc = await _db.Pcs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pcId);
        if (pc == null)
            throw new NotFoundException("PC not found", "PC_NOT_FOUND");

        var statuses = await GetBranchPcStatusesAsync(pc.BranchId);
        return statuses.First(s => s.Id == pcId);
    }

    public async Task BroadcastPcStatusChangeAsync(Guid branchId, Guid pcId)
    {
        await _hubNotifier.BroadcastPcStatusChangeAsync(branchId, pcId);
        _logger.LogInformation("Broadcasted PC status change for PC {PcId} on Branch {BranchId}", pcId, branchId);
    }
}
