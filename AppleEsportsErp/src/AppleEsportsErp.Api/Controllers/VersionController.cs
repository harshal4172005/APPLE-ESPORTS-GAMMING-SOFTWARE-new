using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Services;
using AppleEsportsErp.Application.DTOs;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/versions")]
[Authorize]
public class VersionController : ControllerBase
{
    private readonly IVersionService _versionService;
    private readonly AppDbContext _db;
    private readonly IRemoteBranchControl _remote;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VersionController> _logger;

    public VersionController(
        IVersionService versionService, AppDbContext db, IRemoteBranchControl remote, IWebHostEnvironment env,
        IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<VersionController> logger)
    {
        _versionService = versionService;
        _db = db;
        _remote = remote;
        _env = env;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private Guid CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>
    /// The version this exact process is running right now, read straight from the build's own
    /// assembly (AppleEsportsErp.Api.csproj's &lt;Version&gt;/&lt;AssemblyVersion&gt;) - never a
    /// hardcoded string that can drift, and not the same thing as "latest" below, which is
    /// whatever VersionInfo row was published through the update system and may not be what is
    /// actually installed on this machine. Anonymous and DB-free on purpose: the sidebar renders
    /// this on every screen, and "what version is live" has to answer even if the database is
    /// the thing that's broken.
    /// </summary>
    [HttpGet("running")]
    [AllowAnonymous]
    public IActionResult GetRunningVersion()
    {
        var asmVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var version = asmVersion == null ? "unknown" : $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}";
        return Ok(ApiResponse<object>.Ok(new { version }));
    }

    [HttpGet("latest")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLatestVersion()
    {
        var version = await _versionService.GetLatestVersionAsync();
        if (version == null)
            return Ok(ApiResponse<object>.Ok(null));

        return Ok(ApiResponse<VersionInfoDto>.Ok(version));
    }

    /// <summary>
    /// Readable by any signed-in operator or admin, not just the owner. The whole point of
    /// keeping the history is that the person about to apply an update can see what is in it
    /// first, and that person is the operator.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetVersionHistory()
    {
        var versions = await _versionService.GetVersionHistoryAsync();
        return Ok(ApiResponse<List<VersionInfoDto>>.Ok(versions));
    }

    [HttpGet("branch/{branchId}")]
    public async Task<IActionResult> GetBranchVersionStatus(Guid branchId)
    {
        var status = await _versionService.GetBranchVersionStatusAsync(branchId);
        if (status == null)
            return Ok(ApiResponse<object>.Ok(null));

        return Ok(ApiResponse<BranchVersionStatusDto>.Ok(status));
    }

    [HttpGet("all-branches")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> GetAllBranchVersionStatuses()
    {
        var statuses = await _versionService.GetAllBranchVersionStatusesAsync();
        return Ok(ApiResponse<List<BranchVersionStatusDto>>.Ok(statuses));
    }

    [HttpPost("create")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> CreateVersion([FromBody] CreateVersionDto dto)
    {
        var version = await _versionService.CreateVersionAsync(dto.Version, dto.ReleaseNotes);
        return Ok(ApiResponse<VersionInfoDto>.Ok(version));
    }

    [HttpPost("approve")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> ApproveVersion([FromBody] ApproveUpdateDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var version = await _versionService.ApproveVersionAsync(dto.VersionInfoId, userId);
        return Ok(ApiResponse<VersionInfoDto>.Ok(version));
    }

    /// <summary>
    /// Takes a version off the branches without deleting it - the record, its release notes
    /// and its installer all stay, so it can be approved again later. What "Approve" never had
    /// a way back from.
    /// </summary>
    [HttpPost("{id:int}/unapprove")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> UnapproveVersion(int id)
    {
        var version = await _versionService.UnapproveVersionAsync(id);
        return Ok(ApiResponse<VersionInfoDto>.Ok(version));
    }

    /// <summary>
    /// Removes a version's record entirely, and its installer file with it - what "this should
    /// never have existed" actually needs. Deleting it, rather than only unapproving it, is
    /// also what lets the version before it become "Newest update" again: that display takes
    /// whichever record is newest by creation date with no regard for approval, so an
    /// unapproved-but-undeleted version still shows there.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> DeleteVersion(int id)
    {
        var (installerFileName, agentFileName) = await _versionService.DeleteVersionAsync(id);

        foreach (var fileName in new[] { installerFileName, agentFileName })
        {
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            var path = Path.Combine(ReleasesController.ResolveReleaseFolder(_env), fileName);
            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
            catch { /* the record is gone either way; a leftover file on disk harms nothing */ }
        }

        return Ok(ApiResponse<object>.Ok(new { deleted = true }));
    }

    [HttpPut("branch/{branchId}/auto-update")]
    [Authorize]
    public async Task<IActionResult> UpdateBranchAutoUpdate(Guid branchId, [FromBody] UpdateBranchAutoUpdateDto dto)
    {
        await _versionService.UpdateBranchAutoUpdateAsync(branchId, dto.AutoUpdateEnabled);
        return Ok(ApiResponse<object>.Ok("Auto-update setting updated"));
    }

    [HttpPost("branch/{branchId}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateBranchVersionStatus(Guid branchId, [FromBody] UpdateBranchVersionStatusDto dto)
    {
        await _versionService.UpdateBranchVersionStatusAsync(branchId, dto.CurrentVersion, dto.UpToDateCount, dto.TotalCount);
        return Ok(ApiResponse<object>.Ok("Version status updated"));
    }

    /// <summary>
    /// Where a branch says how its update is going, so the Updates page can show a progress bar
    /// that reflects something real.
    ///
    /// Anonymous for the same reason the status endpoint above is: a branch mid-update may be
    /// restarting its own services and cannot be relied on to hold a session through it. It
    /// writes only progress text against a branch id — there is nothing here worth forging, and
    /// the version a branch is allowed to install is still governed by approval.
    ///
    /// Called by the branch's own desktop app (MainForm.ReportUpdateProgressAsync), which posts
    /// here rather than to Head Office directly: the desktop client has never been told Head
    /// Office's address, this API has it as Sync:HeadOfficeUrl, and writing locally first is what
    /// makes the branch's own Updates page correct with no internet at all.
    /// </summary>
    [HttpPost("branch/{branchId}/progress")]
    [AllowAnonymous]
    public async Task<IActionResult> ReportUpdateProgress(Guid branchId, [FromBody] UpdateProgressDto dto)
    {
        await _versionService.ReportUpdateProgressAsync(branchId, dto.Stage, dto.ProgressPercent, dto.Message);
        await ForwardProgressToHeadOfficeAsync(branchId, dto);
        return Ok(ApiResponse<object>.Ok("Progress recorded"));
    }

    /// <summary>
    /// Passes a branch's update progress up to Head Office, where the Super Admin is watching.
    ///
    /// Awaited rather than left to finish in the background, and that is the point of it. The
    /// most valuable report of the whole sequence is "installing", and the very next thing the
    /// branch does after sending it is stop its own PostgreSQL and API so they can be replaced.
    /// A fire-and-forget send would be racing a shutdown it is guaranteed to lose, so the one
    /// stage that explains a branch going quiet for a minute would be the one that never arrived.
    /// Eight seconds of timeout against that is worth paying.
    ///
    /// Does nothing at Head Office itself, which has no Sync:HeadOfficeUrl and is already the
    /// destination - the local write above was the whole job there.
    ///
    /// Never throws. A branch that cannot reach Head Office still has to be able to update
    /// itself; its own database already has the truth, and the next report will carry it up.
    /// </summary>
    private async Task ForwardProgressToHeadOfficeAsync(Guid branchId, UpdateProgressDto dto)
    {
        var headOffice = _configuration["Sync:HeadOfficeUrl"];
        if (string.IsNullOrWhiteSpace(headOffice)) return;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                stage = dto.Stage,
                progressPercent = dto.ProgressPercent,
                message = dto.Message,
            });

            var response = await client.PostAsync(
                $"{headOffice.TrimEnd('/')}/api/versions/branch/{branchId}/progress",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning(
                    "Head Office refused this branch's update progress ({Stage}): {Status}.",
                    dto.Stage, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not tell Head Office this branch's update progress.");
        }
    }

    /// <summary>
    /// Sends one branch an exact, already-published version to run - the one remote action
    /// that can go backwards. Every other update path in this system only ever moves a branch
    /// forward, because a branch checking on its own schedule (desktop-client's UpdateService)
    /// deliberately refuses anything that is not strictly newer than what it already runs.
    /// This is different: a person at Head Office has decided precisely what a branch should
    /// be running, older or newer, and that decision is allowed to override the branch's own
    /// caution.
    ///
    /// Restricted to Super Admin alone, tighter than every other remote command in this
    /// system. If the branch's own attempt to carry this out fails partway, it can take down
    /// the very service that would have reported the failure - there is no channel left for
    /// Head Office to retry through, only physical or remote-desktop access to that PC. See
    /// BranchHeartbeatService.RunInstallVersionAsync.
    /// </summary>
    [HttpPost("branch/{branchId}/install")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> InstallVersionOnBranch(
        Guid branchId, [FromBody] InstallVersionDto dto, CancellationToken ct)
    {
        if (!_remote.MustTravel)
            return BadRequest(ApiResponse<object>.Fail(
                "This only makes sense from Head Office, telling a branch what to run.",
                "HEAD_OFFICE_ONLY"));

        if (string.IsNullOrWhiteSpace(dto.Version))
            return BadRequest(ApiResponse<object>.Fail("Which version should this branch run?", "VERSION_REQUIRED"));

        var version = await _db.Set<AppleEsportsErp.Domain.Entities.VersionInfo>().AsNoTracking()
            .FirstOrDefaultAsync(v => v.CurrentVersion == dto.Version, ct);

        if (version is null)
            return NotFound(ApiResponse<object>.Fail(
                $"There is no version {dto.Version} on record.", "VERSION_NOT_FOUND"));

        if (string.IsNullOrWhiteSpace(version.InstallerFileName) || string.IsNullOrWhiteSpace(version.InstallerSha256))
            return BadRequest(ApiResponse<object>.Fail(
                $"Version {dto.Version} has no installer published against it, so there is nothing to send.",
                "NO_INSTALLER"));

        var receipt = await _remote.SendAsync(branchId, BranchCommands.InstallVersion, new
        {
            version = version.CurrentVersion,
            sha256 = version.InstallerSha256,
            downloadPath = $"/api/releases/download/{version.InstallerFileName}",
            sizeBytes = version.InstallerSizeBytes,
            reason = dto.Reason,
        }, CurrentUserId(), ct);

        return Accepted(ApiResponse<object>.Ok(new
        {
            queued = true,
            commandId = receipt.CommandId,
            branchIsReporting = receipt.BranchIsReporting,
            message = receipt.Message,
        }));
    }
}

public class UpdateProgressDto
{
    /// <summary>"downloading", "installing", "restarting", "done" or "failed".</summary>
    public string Stage { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public string? Message { get; set; }
}

public class CreateVersionDto
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
}

public class UpdateBranchVersionStatusDto
{
    public string CurrentVersion { get; set; } = string.Empty;
    public int UpToDateCount { get; set; }
    public int TotalCount { get; set; }
}

public class InstallVersionDto
{
    public string Version { get; set; } = string.Empty;

    /// <summary>Why, for whoever reads the audit trail later - "downgrading after 2.4.9 was rolled back", not left blank.</summary>
    public string? Reason { get; set; }
}
