using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Services;

// Mirrors OpenSessionMonitorService's wallet-exhaustion auto-stop, but for the
// other kind of session: a fixed-duration plan (1hr/2hr/3hr) whose purchased
// time has run out. Until this existed, PlannedDurationMin/EndTime were only
// ever used to render a countdown/"Overdue" label — nothing actually stopped
// the session, so it kept running (and billing) indefinitely past the plan.
//
// Branch-only. At Head Office every branch's synced sessions look exactly like local ones, so
// this would stop and re-bill all four shops' customers in a database nobody is standing in.
// See BranchOnlyBackgroundService.
public class FixedDurationSessionMonitorService : BranchOnlyBackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FixedDurationSessionMonitorService> _logger;

    public FixedDurationSessionMonitorService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<FixedDurationSessionMonitorService> logger)
        : base(configuration, logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FixedDurationSessionMonitorService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpiredFixedSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing CheckExpiredFixedSessionsAsync.");
            }

            // Was 15 seconds. A customer whose plan just ran out was still being let play for
            // up to 15s past their own expiry before this even noticed, on top of whatever the
            // overlay itself then takes to catch up and lock - "the lock screen comes after
            // some time" was that stack of two delays, not a broken push. Five is a plain
            // indexed query against a handful of rows at most; nothing about running it three
            // times as often costs anything worth trading against a paying-for-nothing gap.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("FixedDurationSessionMonitorService is stopping.");
    }

    private async Task CheckExpiredFixedSessionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

        var now = DateTimeOffset.UtcNow;

        var expiredSessions = await db.Sessions
            .Where(s => s.State == SessionState.Active
                     && s.PlannedDurationMin != null
                     && s.EndTime != null
                     && s.EndTime <= now)
            .ToListAsync(stoppingToken);

        if (!expiredSessions.Any())
            return;

        foreach (var session in expiredSessions)
        {
            _logger.LogWarning(
                "Auto-stopping expired fixed-duration Session {SessionId} (planned {PlannedMinutes}min, expired at {EndTime}).",
                session.Id, session.PlannedDurationMin, session.EndTime);

            try
            {
                // Same path a human operator's "Stop" click takes: closes the session,
                // finalizes the bill, flips the PC to AwaitingBilling, and sends the
                // lock command to the PC agent — reused as-is, nothing duplicated here.
                await sessionService.StopSessionAsync(session.BranchId, session.OperatorId, session.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-stop expired fixed-duration session {SessionId}", session.Id);
            }
        }
    }
}
