using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Services;

public class ReservationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationBackgroundService> _logger;

    public ReservationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReservationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reservation Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReservationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred processing reservations in background service.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessReservationsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hubNotifier = scope.ServiceProvider.GetRequiredService<IHubNotificationService>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var now = DateTimeOffset.UtcNow;
        bool changed = false;

        // 1. Transition PCs to Reserved if they have an upcoming pending reservation starting in <= 15 minutes
        var upcomingLimit = now.AddMinutes(15);
        var upcomingReservations = await db.Reservations
            .Include(r => r.Pc)
            .Where(r => r.State == ReservationState.Pending 
                     && r.ReservationTime <= upcomingLimit 
                     && r.ReservationTime > now.AddMinutes(-r.GracePeriodMin))
            .ToListAsync();

        var pcStatusChangesToBroadcast = new List<(Guid BranchId, Guid PcId)>();
        var reservationChangesToBroadcast = new List<(Guid BranchId, Guid ResId)>();

        foreach (var res in upcomingReservations)
        {
            var pc = res.Pc;
            if (pc != null && pc.State == PcState.Idle)
            {
                pc.State = PcState.Reserved;
                pc.CurrentReservationId = res.Id;
                db.Pcs.Update(pc);
                changed = true;
                
                pcStatusChangesToBroadcast.Add((res.BranchId, pc.Id));
                reservationChangesToBroadcast.Add((res.BranchId, res.Id));
                _logger.LogInformation("PC {PcNumber} transitioned to Reserved for reservation {ReservationId}", pc.PcNumber, res.Id);
            }
        }

        // 2. Auto-expire reservations whose grace period has ended
        var pendingReservations = await db.Reservations
            .Include(r => r.Pc)
            .Where(r => r.State == ReservationState.Pending)
            .ToListAsync();

        var expiredList = pendingReservations
            .Where(r => r.ReservationTime.AddMinutes(r.GracePeriodMin) < now)
            .ToList();

        foreach (var res in expiredList)
        {
            res.State = ReservationState.Expired;
            res.ExpiredAt = now;
            db.Reservations.Update(res);
            changed = true;

            var pc = res.Pc;
            if (pc != null && pc.State == PcState.Reserved && pc.CurrentReservationId == res.Id)
            {
                pc.State = PcState.Idle;
                pc.CurrentReservationId = null;
                db.Pcs.Update(pc);
                pcStatusChangesToBroadcast.Add((res.BranchId, pc.Id));
            }

            await auditService.LogAsync(new AuditEntry
            {
                OperatorId = Guid.Empty, // System action
                UserRole = "System",
                UserName = "System",
                Action = AuditActions.ReservationExpire,
                BranchId = res.BranchId,
                TargetType = "reservation",
                TargetId = res.Id,
                Details = new { CustomerName = res.CustomerName, ReservationTime = res.ReservationTime, ExpiredAt = now }
            });

            reservationChangesToBroadcast.Add((res.BranchId, res.Id));
            _logger.LogInformation("Reservation {ReservationId} expired automatically.", res.Id);
        }

        if (changed)
        {
            await db.SaveChangesAsync();

            // Broadcast after saving changes successfully
            foreach (var pcChange in pcStatusChangesToBroadcast.Distinct())
            {
                await hubNotifier.BroadcastPcStatusChangeAsync(pcChange.BranchId, pcChange.PcId);
            }
            foreach (var resChange in reservationChangesToBroadcast.Distinct())
            {
                await hubNotifier.BroadcastReservationUpdateAsync(resChange.BranchId, resChange.ResId);
            }
        }
    }
}
