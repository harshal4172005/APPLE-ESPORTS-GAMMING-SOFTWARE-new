using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Services;

/// <summary>
/// The self-healing half of sync.
///
/// Everything else in this system captures a row once, at the moment it changes, and trusts
/// that single capture to eventually reach Head Office (see SyncCapture). That is right for a
/// bill or a login - each happens once, so the capture and the delivery attempt it creates are
/// the same event. Shift, CashRegister and Operator rows are different: a shift or register
/// stays open for hours with nothing else touching it again while it does, and an operator can
/// go quiet for months once created - either way, a single missed capture (a stale JWT claim
/// attaching a new cash register to the wrong shift, a bug not yet found, a crash before the
/// save that would have captured it) had no second chance, because nothing about the row ever
/// changes again to give the capture path another try. That is exactly how a real cash register
/// opened at Citylight sat with zero delivery attempts, forever, until someone opened the
/// database by hand and asked why its opening balance never reached the server - and, separately,
/// how the "system_admin_&lt;branch&gt;" operator Quick Admin Switch creates the first time
/// anyone uses it sat unsynced for good, taking every session, bill and payment it ever touched
/// down with it.
///
/// This asks a much simpler, much more robust question instead of trying to catch every way a
/// capture can be missed: for every row worth re-checking, is a delivery attempt already sitting
/// undelivered in the outbox? If not, queue a fresh one. It does not need to know why the first
/// attempt is missing - it just guarantees the row is never more than one sweep away from
/// another try. SyncInboxController applies these as an upsert keyed on the row's own id, so
/// re-queuing a row that actually arrived fine already is a no-op at the other end, not a
/// duplicate.
///
/// Branch-only, like PcAgentWatchdogService: Head Office's own copy of these rows is what
/// branches sync TO, not a source to sync FROM - see BranchOnlyBackgroundService.
/// </summary>
public class SyncReconciliationService : BranchOnlyBackgroundService
{
    /// <summary>
    /// Fifteen minutes: frequent enough that a missed capture is never stuck for long, cheap
    /// enough that re-checking a handful of open shifts and registers costs nothing between
    /// sweeps.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<SyncReconciliationService> _logger;

    public SyncReconciliationService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<SyncReconciliationService> logger)
        : base(configuration, logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncReconciliationService is starting.");

        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await ResweepAsync(db, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed while re-checking still-open rows for undelivered sync.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("SyncReconciliationService is stopping.");
    }

    private async Task ResweepAsync(AppDbContext db, CancellationToken ct)
    {
        var queued = 0;

        var openShifts = await db.Shifts
            .Where(s => s.Status == ShiftStatus.Active)
            .ToListAsync(ct);
        foreach (var shift in openShifts)
            queued += await RequeueIfNeededAsync(db, shift, ct);

        var openRegisters = await db.CashRegisters
            .Where(r => r.Status != CashRegisterStatus.Closed)
            .ToListAsync(ct);
        foreach (var register in openRegisters)
            queued += await RequeueIfNeededAsync(db, register, ct);

        // Lowercase to match the literal BillingService/SessionService actually write - there
        // is no CreditStatus enum backing this column, just this string.
        var pendingCredits = await db.CustomerCredits
            .Where(c => c.Status == "pending")
            .ToListAsync(ct);
        foreach (var credit in pendingCredits)
            queued += await RequeueIfNeededAsync(db, credit, ct);

        // Operators have no "still open" state to filter by the way a shift or a register
        // does - once created, nothing about a quiet operator ever changes again either, so
        // the same gap applies just as badly. Confirmed live at Citylight 144Hz: the
        // "system_admin_<branch>" operator that GetOperatorIdAsync creates the first time
        // anyone uses Quick Admin Switch is created once, via AddAsync, and never saved again
        // - so if that single capture was missed (this exact operator predated Operator being
        // added to SyncCapture.Watched at all), there was no second chance, ever. Every
        // session, bill and payment it went on to touch sat permanently stuck at Head Office
        // with "no operator X", because the one row that would have unstuck them never had a
        // delivery attempt queued in the first place.
        //
        // Swept in full rather than filtered, since there is no equivalent of "still open" to
        // narrow it by - a branch's whole operator list is a handful of rows, so this costs
        // nothing next to the shift/register sweep above.
        var allOperators = await db.Operators.ToListAsync(ct);
        foreach (var op in allOperators)
            queued += await RequeueIfNeededAsync(db, op, ct);

        if (queued > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Sync reconciliation: {Count} still-open row(s) had no delivery attempt waiting and were re-queued.",
                queued);
        }
    }

    private static async Task<int> RequeueIfNeededAsync<TEntity>(AppDbContext db, TEntity entity, CancellationToken ct)
        where TEntity : class
    {
        var entry = SyncCapture.BuildEntryFor(db.Entry(entity));
        if (entry is null) return 0;

        // Never piles up behind a delivery attempt the courier simply hasn't gotten to yet -
        // this runs every fifteen minutes and must not flood the outbox with competing copies
        // of the exact same row.
        var alreadyQueued = await db.SyncOutboxEntries.AnyAsync(
            e => e.AggregateId == entry.AggregateId
                && e.AggregateType == entry.AggregateType
                && e.SyncedAt == null,
            ct);
        if (alreadyQueued) return 0;

        db.SyncOutboxEntries.Add(entry);
        return 1;
    }
}
