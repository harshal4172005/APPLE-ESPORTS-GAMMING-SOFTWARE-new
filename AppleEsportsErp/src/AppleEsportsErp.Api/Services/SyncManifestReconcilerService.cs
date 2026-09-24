using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Api.Controllers;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Services;

/// <summary>
/// The actual "does Head Office still agree with me" check, run periodically by every branch -
/// at two levels.
///
/// SyncReconciliationService (see its own remarks) guarantees a still-open row is never more
/// than one sweep away from a delivery attempt - but it only ever asks "is a delivery already
/// queued for this row", which says nothing about whether a row that was already delivered and
/// applied still matches what the branch holds right now. A correction made to an
/// already-closed register, or any future code path that changes a watched row without going
/// through whatever SyncCapture happens to be hooked into, would leave Head Office holding a
/// stale copy indefinitely with nothing anywhere flagging it - exactly the "operator database
/// and server database should be completely the same" gap this was asked to close.
///
/// The first level is the row check: every watched row this branch has recently cared about is
/// fingerprinted fresh, right now, and the fingerprints are sent to Head Office, which checks
/// each one against its own copy of the same row and hands back exactly the ones that disagree -
/// missing, or present but different. Only those get resent in full.
///
/// The second level is the headline check: the exact numbers a branch's own dashboard shows -
/// PC states, who is on duty, today's EOD totals - computed the same way at Head Office from its
/// mirrored copy of this branch, and compared side by side. This is the closest thing to
/// literally comparing what is on screen at the counter against what is on screen at Head
/// Office, without needing either screen open. A mismatch here alongside a row-level mismatch is
/// just that fix still in flight; a mismatch here with no row-level mismatch means the
/// underlying data already agrees and something in how one side computes or shows it does not -
/// worth investigating as a real bug rather than something a resync will fix.
///
/// Both levels log a plain summary of what did not match, so a discrepancy is visible
/// immediately rather than found by opening the database by hand.
///
/// Branch-only: Head Office has nothing of its own to compare itself against - see
/// BranchOnlyBackgroundService.
/// </summary>
public class SyncManifestReconcilerService : BranchOnlyBackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How far back to check. Bounded on purpose: re-fingerprinting the branch's entire history
    /// every half hour costs nothing for a shop open a few months, but would eventually mean
    /// hashing years of long-closed registers for no reason. Nothing that old is still being
    /// argued over - this window only ever needs to catch drift from the recent past.
    /// </summary>
    private static readonly TimeSpan Window = TimeSpan.FromDays(14);

    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SyncManifestReconcilerService> _logger;

    public SyncManifestReconcilerService(
        IServiceProvider services,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<SyncManifestReconcilerService> logger)
        : base(configuration, logger)
    {
        _services = services;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncManifestReconcilerService is starting.");

        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var branchId = await ReconcileAsync(db, stoppingToken);

                if (branchId is { } id)
                {
                    var eodService = scope.ServiceProvider.GetRequiredService<IEodService>();
                    await CheckParityAsync(db, eodService, id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed while reconciling this branch's data against Head Office.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("SyncManifestReconcilerService is stopping.");
    }

    /// <summary>
    /// Returns the branch's own id on any outcome where that id is actually known - including
    /// "nothing to check" and "Head Office refused/could not be reached" - so
    /// <see cref="CheckParityAsync"/> still gets a chance to run this sweep even when there was
    /// no row-level manifest to send. Returns null only when there is nowhere to report to or no
    /// branch identity yet, the two cases where neither check can do anything at all.
    /// </summary>
    private async Task<Guid?> ReconcileAsync(AppDbContext db, CancellationToken ct)
    {
        var headOffice = _configuration["Sync:HeadOfficeUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(headOffice)) return null;

        // One branch row, its own, once adopted. Ordered the same way BranchHeartbeatService
        // reads it, so a local database holding more than one row for some reason still reports
        // itself consistently rather than picking a different one between sweeps.
        var branchId = await db.Branches.AsNoTracking()
            .OrderBy(b => b.Id).Select(b => b.Id).FirstOrDefaultAsync(ct);
        if (branchId == Guid.Empty) return null;

        var cutoff = DateTime.UtcNow - Window;

        // Every distinct row this branch has captured recently - the outbox's own history is
        // exactly the list of "rows that have ever mattered enough to sync", so there is nothing
        // type-specific to know here about which column means "recent" for a shift versus a
        // bill versus a credit.
        var candidates = await db.SyncOutboxEntries
            .Where(e => e.CreatedAt >= cutoff)
            .Select(e => new { e.AggregateType, e.AggregateId })
            .Distinct()
            .ToListAsync(ct);

        if (candidates.Count == 0) return branchId;

        var manifest = new List<object>();
        foreach (var c in candidates)
        {
            var type = SyncCapture.TypeForAggregate(c.AggregateType);
            if (type is null) continue;

            var current = await db.FindAsync(type, new object?[] { c.AggregateId }, ct);
            if (current is null) continue;   // deleted locally since; nothing left to compare

            manifest.Add(new
            {
                aggregateType = c.AggregateType,
                aggregateId = c.AggregateId,
                checksum = SyncCapture.ComputeChecksum(db.Entry(current)),
            });
        }

        if (manifest.Count == 0) return branchId;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(
                $"{headOffice}/api/sync/reconcile",
                new StringContent(
                    JsonSerializer.Serialize(new { branchId, entries = manifest }),
                    Encoding.UTF8, "application/json"),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reach Head Office to reconcile - will try again next sweep.");
            return branchId;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Head Office refused the reconciliation manifest: {Status}", response.StatusCode);
            return branchId;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var wrapped = JsonSerializer.Deserialize<ApiEnvelope>(body, JsonOptions);
        var mismatched = wrapped?.Data?.Mismatched ?? new List<ReconcileMismatch>();

        if (mismatched.Count == 0)
        {
            _logger.LogInformation(
                "Sync reconciliation: checked {Count} row(s) against Head Office - all agree.",
                manifest.Count);
            return branchId;
        }

        _logger.LogWarning(
            "Sync reconciliation: {Count} row(s) disagree with Head Office and are being resent: {Rows}",
            mismatched.Count,
            string.Join(", ", mismatched.Select(m => $"{m.AggregateType}:{m.AggregateId} ({m.Reason})")));

        var requeued = 0;
        foreach (var m in mismatched)
        {
            var type = SyncCapture.TypeForAggregate(m.AggregateType);
            if (type is null) continue;

            var current = await db.FindAsync(type, new object?[] { m.AggregateId }, ct);
            if (current is null) continue;

            var entry = SyncCapture.BuildEntryFor(db.Entry(current));
            if (entry is null) continue;

            db.SyncOutboxEntries.Add(entry);
            requeued++;
        }

        if (requeued > 0) await db.SaveChangesAsync(ct);

        return branchId;
    }

    /// <summary>
    /// The headline check. Computes this branch's own answer to "what would my dashboard show
    /// right now" using the exact same method Head Office uses to compute its own answer for
    /// this branch (<see cref="SyncInboxController.BuildParitySnapshotAsync"/>), then compares
    /// the two field by field.
    /// </summary>
    private async Task CheckParityAsync(AppDbContext db, IEodService eodService, Guid branchId, CancellationToken ct)
    {
        var headOffice = _configuration["Sync:HeadOfficeUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(headOffice)) return;

        var ours = await SyncInboxController.BuildParitySnapshotAsync(db, eodService, branchId);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync($"{headOffice}/api/sync/parity-snapshot?branchId={branchId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reach Head Office for a parity check - will try again next sweep.");
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Head Office refused the parity check: {Status}", response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var wrapped = JsonSerializer.Deserialize<ParityEnvelope>(body, JsonOptions);
        var theirs = wrapped?.Data;
        if (theirs is null) return;

        var diffs = new List<string>();
        void Compare(string name, object ourValue, object theirValue)
        {
            if (!Equals(ourValue, theirValue))
                diffs.Add($"{name} (branch={ourValue}, headOffice={theirValue})");
        }

        Compare("TotalPcs", ours.TotalPcs, theirs.TotalPcs);
        Compare("PcsActive", ours.PcsActive, theirs.PcsActive);
        Compare("PcsAwaitingBilling", ours.PcsAwaitingBilling, theirs.PcsAwaitingBilling);
        Compare("PcsIdle", ours.PcsIdle, theirs.PcsIdle);
        Compare("PcsMaintenance", ours.PcsMaintenance, theirs.PcsMaintenance);
        Compare("OperatorsOnDuty", ours.OperatorsOnDuty, theirs.OperatorsOnDuty);
        Compare("EodNetRevenue", ours.EodNetRevenue, theirs.EodNetRevenue);
        Compare("EodGamingRevenue", ours.EodGamingRevenue, theirs.EodGamingRevenue);
        Compare("EodFoodRevenue", ours.EodFoodRevenue, theirs.EodFoodRevenue);
        Compare("EodCashTotal", ours.EodCashTotal, theirs.EodCashTotal);
        Compare("EodOnlineTotal", ours.EodOnlineTotal, theirs.EodOnlineTotal);
        Compare("ExpectedDrawerCash", ours.ExpectedDrawerCash, theirs.ExpectedDrawerCash);

        if (diffs.Count == 0)
        {
            _logger.LogInformation("Parity check: this branch's headline numbers match Head Office's.");
            return;
        }

        // This is the case that matters most: the row-level check above already confirmed the
        // underlying data agrees (or is being fixed if it does not), so a headline number that
        // still disagrees is not something a resync will fix - it means one side is computing
        // or displaying this number differently, which needs a person to go and look.
        _logger.LogWarning(
            "Parity check: this branch's headline numbers disagree with Head Office - {Diffs}",
            string.Join("; ", diffs));
    }

    // Case-insensitive on purpose: this reads a response produced by ASP.NET Core's own output
    // formatter on the other end, not written by this codebase, so its exact casing convention
    // is not something to depend on here.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private class ParityEnvelope
    {
        public ParitySnapshotDto? Data { get; set; }
    }

    private class ApiEnvelope
    {
        public ReconcileResult? Data { get; set; }
    }

    private class ReconcileResult
    {
        public List<ReconcileMismatch> Mismatched { get; set; } = new();
    }

    private class ReconcileMismatch
    {
        public string AggregateType { get; set; } = "";
        public Guid AggregateId { get; set; }
        public string Reason { get; set; } = "";
    }
}
