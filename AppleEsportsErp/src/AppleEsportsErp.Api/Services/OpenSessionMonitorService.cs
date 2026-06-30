using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Services;

public class OpenSessionMonitorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OpenSessionMonitorService> _logger;

    public OpenSessionMonitorService(IServiceProvider services, ILogger<OpenSessionMonitorService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OpenSessionMonitorService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOpenSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing CheckOpenSessionsAsync.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("OpenSessionMonitorService is stopping.");
    }

    private async Task CheckOpenSessionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

        var now = DateTimeOffset.UtcNow;

        // Find active open sessions for members
        var openSessions = await db.Sessions
            .Include(s => s.Pc)
            .Where(s => s.State == SessionState.Active && s.PlannedDurationMin == null && s.MemberId != null)
            .ToListAsync(stoppingToken);

        if (!openSessions.Any())
            return;

        foreach (var session in openSessions)
        {
            var member = await db.Members.FindAsync(new object[] { session.MemberId! }, stoppingToken);
            if (member == null) continue;

            var actualDurationMin = (now - session.StartTime).TotalMinutes;
            decimal ratePerHour = 80m; // Member rate
            decimal hours = Math.Max((decimal)actualDurationMin / 60m, 1m / 60m);
            decimal accruedCost = Math.Round(hours * ratePerHour, 2);

            // If accrued cost >= their balance, terminate session forcefully!
            if (accruedCost >= member.GamingBalance)
            {
                _logger.LogWarning("Forcefully stopping Open Session {SessionId} due to insufficient wallet balance. Accrued: {Cost}, Wallet: {Wallet}", 
                    session.Id, accruedCost, member.GamingBalance);

                try
                {
                    // Stop Session - this will automatically do the deduction because of our previous changes
                    await sessionService.StopSessionAsync(session.BranchId, session.OperatorId, session.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to forcefully stop session {SessionId}", session.Id);
                }
            }
        }
    }
}
