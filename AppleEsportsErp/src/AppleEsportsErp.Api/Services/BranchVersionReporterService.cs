using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Configuration;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Services;

/// <summary>
/// Tells Head Office which version this branch is actually running.
///
/// Updates had two halves and only one of them existed. Head Office could publish a version and
/// approve it, and a branch could fetch and install it — but nothing ever reported back, so
/// `BranchVersionStatuses` was empty and the Updates page could not say what any branch was on.
/// An update system you cannot see the results of is barely an update system: you would push a
/// fix to four shops and have no way of knowing whether any of them took it, which is exactly
/// the moment you most want to know.
///
/// Runs on branches only. Head Office is not a branch and has nobody to report to.
///
/// Failures are deliberately quiet. This is telemetry: a branch whose line is down must carry on
/// trading and simply say so at the next opportunity, not log an error every minute for
/// something that costs nothing while it waits.
/// </summary>
public class BranchVersionReporterService : BackgroundService
{
    private readonly ILogger<BranchVersionReporterService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;

    // Every two minutes, not every fifteen. Fifteen was chosen for something that changes
    // perhaps once a month, which is true of the VERSION but not of what a person watching the
    // screen expects: after an update they look straight away, and being told "not reported
    // yet" for a quarter of an hour reads as broken rather than as pending. The call is a few
    // hundred bytes.
    private static readonly TimeSpan ReportEvery = TimeSpan.FromMinutes(2);

    public BranchVersionReporterService(
        ILogger<BranchVersionReporterService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// The version of the running build, taken from the assembly rather than a constant, so it
    /// cannot drift from what was actually installed. A hand-maintained version string is how
    /// the app came to report 2.0 for months while 2.2 was on the machine.
    /// </summary>
    private static string RunningVersion =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "0.0.0";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_configuration.IsHeadOffice())
        {
            _logger.LogInformation(
                "This instance is Head Office, so it reports its version to nobody. " +
                "It is branches that report upward.");
            return;
        }

        // Long enough for the database to be accepting connections, short enough that somebody
        // watching after an update is not left staring at a stale screen. A branch PC starting
        // after a power cut brings several services up at once, so this is not instant.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not report this branch's version. Will try again.");
            }

            await Task.Delay(ReportEvery, stoppingToken);
        }
    }

    private async Task ReportOnceAsync(CancellationToken ct)
    {
        var headOffice = _configuration["Sync:HeadOfficeUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(headOffice)) return;

        using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // A branch holds exactly one branch row — its own — once it has been adopted. Before
        // adoption it holds none, and there is nothing to report yet: an unadopted branch has no
        // identity Head Office would recognise.
        //
        // Ordered, the same way BranchHeartbeatService.BeatAsync already is and for the same
        // reason: a branch whose local database somehow holds more than one row must still
        // report the same one every time. Unordered, this and BeatAsync are free to pick
        // different rows from each other on the same branch - which is exactly how a PC count
        // taken against one row (35, here) ended up disagreeing with the Sessions grid, built
        // from the other (16, the real one).
        var branch = await db.Branches.AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(ct);

        if (branch is null)
        {
            _logger.LogDebug("Not adopted yet, so there is no branch identity to report against.");
            return;
        }

        // How many of this branch's gaming PCs are on the current version. Counted from
        // Pc.AppVersion - what AppleEsports.exe itself last reported (PublicController's
        // pcs/{id}/app-version, called from MainForm.cs every 30 seconds) - not Pc.AgentVersion,
        // which is a second, wholly independent program on the same machine (the screen-lock
        // agent, AppleEsportsAgent.exe) with its own installer component and its own self-update
        // path. Counting against AgentVersion meant a gaming PC could update for real - the
        // program a customer actually plays through - and this count would still say it had
        // not, sometimes for good, if that one release's agent build was never uploaded or the
        // agent's own update simply had not landed yet. AppVersion is what "the gaming PC got
        // updated" actually means to somebody looking at the screen.
        //
        // AwaitingSetup rows are excluded from the denominator too. Those are seats nobody has
        // physically claimed yet - a bulk-created placeholder, not a real machine - and counting
        // them here is why this page could read "0 of 35 up to date" against a branch that only
        // has sixteen real gaming PCs on the floor: the other nineteen were never going to report
        // a version because there is no app running on them to report one.
        var totalPcs = await db.Pcs.CountAsync(
            p => p.BranchId == branch.Id && !p.IsDeleted && p.State != PcState.AwaitingSetup, ct);
        var upToDatePcs = await db.Pcs.CountAsync(
            p => p.BranchId == branch.Id && !p.IsDeleted && p.State != PcState.AwaitingSetup
                 && p.AppVersion == RunningVersion, ct);

        // Written to the branch's OWN database first, before Head Office is even contacted.
        //
        // The branch's Updates page reads locally, and nothing local ever filled this in - the
        // reporter only sent upward - so a counter PC said "Not reported yet" for ever while
        // Head Office showed the version perfectly. It was reading a drawer nothing put
        // anything into.
        //
        // Local first also means it is right with no internet, which is the state a branch is
        // designed to survive. What version am I running is a question a shop can always answer
        // about itself; it should never depend on a line to Head Office.
        var versions = scope.ServiceProvider.GetRequiredService<IVersionService>();
        try
        {
            await versions.UpdateBranchVersionStatusAsync(branch.Id, RunningVersion, upToDatePcs, totalPcs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record this branch's own version locally.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            currentVersion = RunningVersion,
            upToDateCount = upToDatePcs,
            totalCount = totalPcs,
        });

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);

        var response = await client.PostAsync(
            $"{headOffice}/api/versions/branch/{branch.Id}/status",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            ct);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "Reported to Head Office: {Branch} is running {Version}.", branch.Name, RunningVersion);
        }
        else
        {
            _logger.LogWarning(
                "Head Office refused this branch's version report: {Status}.", response.StatusCode);
        }
    }
}
