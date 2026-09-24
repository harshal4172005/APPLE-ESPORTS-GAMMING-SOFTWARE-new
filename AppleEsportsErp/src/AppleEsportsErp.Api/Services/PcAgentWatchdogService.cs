using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Services;

/// <summary>
/// Puts a PC back to "Not Set Up" once its agent has genuinely gone quiet, rather than leaving
/// it reading Idle/Free forever because the exe was uninstalled (or the machine retired) with
/// nothing ever telling the server so.
///
/// A PC is claimed exactly once, by AgentController's provision endpoint - that is what flips
/// AwaitingSetup to Idle in the first place. Nothing before this undid that when the physical
/// machine stopped existing: uninstalling the agent removes files from a Windows PC, it does
/// not call anything, so the record it left behind (MachineId, MachineToken, State = Idle) sat
/// there exactly as if the machine still was.
///
/// Genuinely still-installed agents are never at risk here: DualConnectionService heartbeats
/// every 10 seconds and fails over from LAN to Cloud within 30 (AgentConfig.HealthCheckIntervalSeconds/
/// FailoverThresholdSeconds), so anything actually running reports in from one channel or the
/// other well inside a minute of any disruption.
///
/// StaleAfter was originally 5 minutes - "an order of magnitude beyond" that 30-second failover
/// window, on the assumption that the agent itself was reliable and only a genuine uninstall or
/// reboot would ever cause a real gap. That assumption held right up until it met a known-buggy
/// agent build: one that connects, then drops, then reconnects, on its own schedule - not
/// uninstalled, just unreliable. Every drop past 5 minutes wiped the PC's claimed status back to
/// "Not Set Up" and Head Office's view of it along with it, even while an operator was actively
/// running real sessions on that exact machine through the branch's own console. Confirmed live
/// at Citylight 144Hz: every one of sixteen PCs sat permanently "AwaitingSetup" at Head Office,
/// the direct cause of "the whole app isn't syncing, I can't see any sessions on the server."
///
/// 4 hours instead. Long enough that a flaky agent's drop-and-reconnect cycles no longer trip
/// this at all, short enough that a machine genuinely retired or never coming back is still
/// caught the same day rather than sitting stale for good.
///
/// Branch-only, like every other job that acts on live PC state - see BranchOnlyBackgroundService.
/// Head Office's own copy of this same PC is overwritten wholesale by the branch's own heartbeat
/// (BranchHeartbeatController.ApplyPcStatesAsync) every few seconds regardless, so correcting it
/// here on the branch is what corrects Head Office's view too, without a second copy of this job
/// needing to run there and risk disagreeing with the branch about its own machines.
/// </summary>
public class PcAgentWatchdogService : BranchOnlyBackgroundService
{
    /// <summary>Checked this often - frequent enough that "uninstalled" reflects within minutes,
    /// cheap enough that it costs nothing on a branch with sixteen PCs and four sessions running.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long a PC can go without a heartbeat before it is treated as no longer actually
    /// installed. See the class remarks for why this is 4 hours rather than the original 5
    /// minutes.
    /// </summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(4);

    private readonly IServiceProvider _services;
    private readonly ILogger<PcAgentWatchdogService> _logger;

    public PcAgentWatchdogService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<PcAgentWatchdogService> logger)
        : base(configuration, logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PcAgentWatchdogService is starting.");

        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
                var hub = scope.ServiceProvider.GetRequiredService<IHubNotificationService>();

                await ResetStaleAgentsAsync(db, audit, hub, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed while checking for gaming PCs whose agent has gone quiet.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("PcAgentWatchdogService is stopping.");
    }

    private async Task ResetStaleAgentsAsync(
        AppDbContext db, IAuditService audit, IHubNotificationService hub, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - StaleAfter;

        // Never a PC mid-session or mid-bill - a customer sitting at it is what matters, not
        // whether its heartbeat happened to lag behind a busy few minutes. Those are billed and
        // closed by the machinery that already exists for that; this only ever touches a PC that
        // claims to be free while quietly not actually being there any more.
        var stale = await db.Pcs
            .Where(p => !p.IsDeleted
                && p.IsAgentOnline
                && p.State != PcState.Active
                && p.State != PcState.AwaitingBilling
                && p.LastAgentHeartbeat != null
                && p.LastAgentHeartbeat < cutoff)
            .ToListAsync(ct);

        if (stale.Count == 0) return;

        foreach (var pc in stale)
        {
            var quietFor = DateTimeOffset.UtcNow - pc.LastAgentHeartbeat!.Value;

            pc.MachineId = null;
            pc.MachineToken = null;
            pc.ProvisionedAt = null;
            pc.IsAgentOnline = false;
            pc.ConnectionMode = "None";
            pc.AgentVersion = null;
            pc.State = PcState.AwaitingSetup;
            pc.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogWarning(
                "{PcNumber} ({BranchId}) had not heartbeated in {Minutes:0} minutes - reset to " +
                "Not Set Up. Its agent will re-provision on its own if the machine comes back.",
                pc.PcNumber, pc.BranchId, quietFor.TotalMinutes);

            await audit.LogAsync(new AuditEntry
            {
                UserRole = "System",
                UserName = "System",
                Action = "pc_agent_auto_deprovisioned",
                BranchId = pc.BranchId,
                TargetType = "pc",
                TargetId = pc.Id,
                Details = new { pc.PcNumber, quietForMinutes = Math.Round(quietFor.TotalMinutes, 1) },
            });
        }

        await db.SaveChangesAsync(ct);

        foreach (var pc in stale)
        {
            try { await hub.BroadcastPcStatusChangeAsync(pc.BranchId, pc.Id); }
            catch { /* best effort - the next poll picks up the change regardless */ }
        }
    }
}
