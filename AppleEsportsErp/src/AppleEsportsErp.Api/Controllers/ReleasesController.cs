using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Infrastructure.Configuration;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api.Controllers;

/// <summary>
/// Where a branch fetches the installer for a new version, and where Head Office publishes one.
///
/// The tracking half of updates has existed since Phase 1: a version can be created, approved,
/// and every branch's progress watched. The delivery half did not. The dashboard could say
/// "2.2.0 is ready" and pressing Update Now had nothing to fetch, because nothing packaged a
/// build, nothing hosted it, and nothing on a branch could download it. Every fix therefore
/// meant somebody walking to each counter with a USB stick and reinstalling.
///
/// The branch side was already written and is recovered intact - it checks, downloads, verifies
/// a SHA-256 and installs. It was asking <c>/api/releases/latest</c>, which did not exist; the
/// server only had <c>/api/versions/latest</c>, a different route with a different shape. So the
/// client got a 404 every time and silently concluded there was no update. That is this file.
/// </summary>
[ApiController]
[Route("api/releases")]
public class ReleasesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReleasesController> _logger;

    public ReleasesController(
        AppDbContext db,
        IWebHostEnvironment env,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ReleasesController> logger)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Head Office's address, or null when this instance IS Head Office.
    ///
    /// A branch answers these two endpoints by asking Head Office and passing the answer
    /// straight through. That is what makes one address work everywhere: the counter PC's app
    /// talks to the counter PC, a gaming PC talks to the counter PC, and neither has to know
    /// or store where Head Office lives. The branch already knows, because it reports there.
    ///
    /// It also means the shop behaves correctly with no internet: the branch cannot reach Head
    /// Office, so it answers "no update", which is exactly true.
    /// </summary>
    private string? UpstreamHeadOffice =>
        _configuration.IsHeadOffice() ? null : _configuration["Sync:HeadOfficeUrl"]?.TrimEnd('/');

    /// <summary>
    /// Where published installers are kept. Outside the application directory on purpose: an
    /// upgrade replaces the application, and anything stored inside it goes with it.
    /// </summary>
    private string ReleaseFolder => ResolveReleaseFolder(_env);

    /// <summary>
    /// Public so VersionController's delete can clean up an installer file without inventing
    /// a second opinion on where releases live - there is exactly one answer to that question,
    /// and it belongs here rather than duplicated wherever else needs it.
    /// </summary>
    public static string ResolveReleaseFolder(IWebHostEnvironment env)
    {
        var configured = Environment.GetEnvironmentVariable("Releases__Path");
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetPathRoot(env.ContentRootPath) ?? "/", "releases")
            : configured;
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// What a branch asks for, on its own schedule.
    ///
    /// Anonymous, deliberately. A branch checks for updates as a machine rather than as a
    /// logged-in person, and there may be nobody signed in at the counter at 4am. It gives away
    /// nothing but a version number and a hash — both of which a branch has to be told anyway
    /// before it can verify what it downloads.
    /// </summary>
    [HttpGet("latest")]
    [AllowAnonymous]
    public async Task<IActionResult> Latest(CancellationToken ct)
    {
        if (UpstreamHeadOffice is { } upstream)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                var body = await client.GetStringAsync($"{upstream}/api/releases/latest", ct);
                return Content(body, "application/json");
            }
            catch (Exception ex)
            {
                // No internet, or Head Office is down. "No update" is the honest answer and the
                // safe one - the shop carries on with the version it has, which is the whole
                // point of a branch that runs on its own.
                _logger.LogInformation(
                    "Could not ask Head Office about updates ({Reason}). Reporting none available.",
                    ex.GetBaseException().Message);
                return Ok(ApiResponse<object>.Ok(new { available = false }));
            }
        }

        var version = await _db.Set<VersionInfo>()
            .Where(v => v.ApprovedForRollout)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // Nothing approved, or approved but never packaged. Both mean "no update to take", and
        // both must say so plainly rather than offering something a branch cannot fetch.
        if (version is null
            || string.IsNullOrWhiteSpace(version.InstallerFileName)
            || string.IsNullOrWhiteSpace(version.InstallerSha256))
        {
            return Ok(ApiResponse<object>.Ok(new { available = false }));
        }

        // Approved and recorded, but the file is not actually on disk — a database restored
        // without its releases, or an upload that failed halfway. Offering it would send every
        // branch to a download that 404s, on repeat.
        if (!System.IO.File.Exists(Path.Combine(ReleaseFolder, version.InstallerFileName)))
        {
            _logger.LogError(
                "Version {Version} is approved and has an installer recorded, but {FileName} is not on disk. " +
                "Branches are being told there is no update, which is better than sending them to a 404.",
                version.CurrentVersion, version.InstallerFileName);
            return Ok(ApiResponse<object>.Ok(new { available = false }));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            available = true,
            version = version.CurrentVersion,
            releaseNotes = version.ReleaseNotes ?? string.Empty,
            sha256 = version.InstallerSha256,
            sizeBytes = version.InstallerSizeBytes,
            downloadPath = $"/api/releases/download/{version.InstallerFileName}",
        }));
    }

    /// <summary>
    /// What a gaming PC's own agent asks, on its own schedule — the sibling of <see cref="Latest"/>
    /// for the standalone agent exe rather than the branch installer.
    ///
    /// A gaming PC never installs the ~165MB branch installer: it has no database, no API and no
    /// Windows service to update, only its own single exe. Publishing that exe as a separate
    /// artifact against the same <see cref="VersionInfo"/> row is what lets a gaming PC update
    /// itself in seconds over the shop LAN, and what lets a version with no agent changes publish
    /// with nothing here to fetch.
    ///
    /// Anonymous for the same reason <see cref="Latest"/> is: the agent checks as a machine, not a
    /// signed-in person, and there is no login screen running at the customer seat to hold a
    /// session through it.
    /// </summary>
    [HttpGet("agent-latest")]
    [AllowAnonymous]
    public async Task<IActionResult> AgentLatest(CancellationToken ct)
    {
        if (UpstreamHeadOffice is { } upstream)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                var body = await client.GetStringAsync($"{upstream}/api/releases/agent-latest", ct);
                return Content(body, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                    "Could not ask Head Office about agent updates ({Reason}). Reporting none available.",
                    ex.GetBaseException().Message);
                return Ok(ApiResponse<object>.Ok(new { available = false }));
            }
        }

        var version = await _db.Set<VersionInfo>()
            .Where(v => v.ApprovedForRollout)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (version is null
            || string.IsNullOrWhiteSpace(version.AgentFileName)
            || string.IsNullOrWhiteSpace(version.AgentSha256))
        {
            return Ok(ApiResponse<object>.Ok(new { available = false }));
        }

        if (!System.IO.File.Exists(Path.Combine(ReleaseFolder, version.AgentFileName)))
        {
            _logger.LogError(
                "Version {Version} is approved and has an agent exe recorded, but {FileName} is not on disk. " +
                "Gaming PCs are being told there is no update, which is better than sending them to a 404.",
                version.CurrentVersion, version.AgentFileName);
            return Ok(ApiResponse<object>.Ok(new { available = false }));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            available = true,
            version = version.CurrentVersion,
            releaseNotes = version.ReleaseNotes ?? string.Empty,
            sha256 = version.AgentSha256,
            sizeBytes = version.AgentSizeBytes,
            downloadPath = $"/api/releases/download/{version.AgentFileName}",
        }));
    }

    /// <summary>
    /// Hands over the installer itself.
    ///
    /// The branch checks the SHA-256 against what <see cref="Latest"/> published before running
    /// anything, so this endpoint is not what makes the download trustworthy — the hash is. It
    /// still refuses anything that is not a plain file name, because a path that walks upward
    /// would serve any file on the machine to anyone who asked.
    /// </summary>
    [HttpGet("download/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(string fileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName != Path.GetFileName(fileName)
            || fileName.Contains("..", StringComparison.Ordinal))
        {
            return BadRequest(ApiResponse<object>.Fail("That is not a release file name.", "BAD_FILE_NAME"));
        }

        var path = Path.Combine(ReleaseFolder, fileName);

        // A branch fetches the installer from Head Office once, keeps it, and serves it to its
        // own gaming PCs over the shop network. Four PCs at a branch then cost Head Office one
        // download rather than four, and the second gaming PC is served from the counter at LAN
        // speed. The file is verified by the branch's own client against the published hash
        // either way, so a cached copy is no more trusted than a fresh one.
        if (UpstreamHeadOffice is { } upstream && !System.IO.File.Exists(path))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(15);   // installers are large

                await using var incoming = await client.GetStreamAsync(
                    $"{upstream}/api/releases/download/{fileName}", ct);

                var staging = path + ".part";
                await using (var target = System.IO.File.Create(staging))
                {
                    await incoming.CopyToAsync(target, ct);
                }
                System.IO.File.Move(staging, path, overwrite: true);

                _logger.LogInformation("Fetched {FileName} from Head Office for this branch.", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not fetch {FileName} from Head Office.", fileName);
                return StatusCode(502, ApiResponse<object>.Fail(
                    "Could not fetch the update from Head Office.", "HEAD_OFFICE_UNREACHABLE"));
            }
        }

        if (!System.IO.File.Exists(path))
            return NotFound(ApiResponse<object>.Fail("No such release.", "RELEASE_NOT_FOUND"));

        // Streamed rather than read into memory: these are 150 MB and up, and four branches may
        // ask at once.
        return PhysicalFile(path, "application/octet-stream", fileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// Publishes an installer against a version, and records its hash.
    ///
    /// The hash is computed here from the bytes actually stored, never taken from whoever
    /// uploaded. A hash supplied alongside the file proves nothing — the same person could send
    /// both.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [RequestSizeLimit(1_073_741_824)]
    // RequestSizeLimit alone was not enough: it caps Kestrel's overall request body, but
    // ASP.NET Core's multipart/form-data parser enforces its own separate 128MB default on
    // top of that - a branch installer is ~164MB, so every upload failed with "Multipart body
    // length limit exceeded" even after nginx and RequestSizeLimit were both raised.
    [RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string version, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No installer was uploaded.", "NO_FILE"));

        if (string.IsNullOrWhiteSpace(version))
            return BadRequest(ApiResponse<object>.Fail("Which version is this installer for?", "NO_VERSION"));

        var record = await _db.Set<VersionInfo>()
            .FirstOrDefaultAsync(v => v.CurrentVersion == version, ct);

        if (record is null)
            return NotFound(ApiResponse<object>.Fail(
                $"There is no version {version} to attach this to. Create it first.", "VERSION_NOT_FOUND"));

        var safeName = Path.GetFileName(file.FileName);
        if (!safeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail("A release must be an .exe installer.", "NOT_AN_INSTALLER"));

        var target = Path.Combine(ReleaseFolder, safeName);

        // Written to a temporary name and moved into place, so a branch checking at the wrong
        // moment cannot download a half-written installer and fail its hash check.
        var staging = target + ".part";
        await using (var stream = System.IO.File.Create(staging))
        {
            await file.CopyToAsync(stream, ct);
        }

        string hash;
        await using (var stream = System.IO.File.OpenRead(staging))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        }

        System.IO.File.Move(staging, target, overwrite: true);

        record.InstallerFileName = safeName;
        record.InstallerSha256 = hash;
        record.InstallerSizeBytes = file.Length;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Published installer {FileName} ({SizeMb:N0} MB) for version {Version}. SHA-256 {Hash}.",
            safeName, file.Length / 1024d / 1024d, version, hash);

        return Ok(ApiResponse<object>.Ok(new
        {
            version = record.CurrentVersion,
            fileName = safeName,
            sha256 = hash,
            sizeBytes = file.Length,
            approved = record.ApprovedForRollout,
        }));
    }

    /// <summary>
    /// Publishes the standalone gaming-PC agent exe against a version — the sibling of
    /// <see cref="Upload"/> for <see cref="AgentLatest"/> to serve.
    ///
    /// A separate endpoint rather than a second file on the same call: the two artifacts are
    /// built by separate steps of <c>build-branch-installer.ps1</c> and published independently,
    /// and a version with no agent-side change (a pure server fix, say) can be shipped with only
    /// the installer uploaded and agents correctly told there is nothing for them to fetch.
    /// </summary>
    [HttpPost("upload-agent")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [RequestSizeLimit(209_715_200)]
    [RequestFormLimits(MultipartBodyLengthLimit = 209_715_200)]
    public async Task<IActionResult> UploadAgent([FromForm] IFormFile file, [FromForm] string version, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No agent exe was uploaded.", "NO_FILE"));

        if (string.IsNullOrWhiteSpace(version))
            return BadRequest(ApiResponse<object>.Fail("Which version is this agent exe for?", "NO_VERSION"));

        var record = await _db.Set<VersionInfo>()
            .FirstOrDefaultAsync(v => v.CurrentVersion == version, ct);

        if (record is null)
            return NotFound(ApiResponse<object>.Fail(
                $"There is no version {version} to attach this to. Create it first.", "VERSION_NOT_FOUND"));

        var safeName = Path.GetFileName(file.FileName);
        if (!safeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<object>.Fail("An agent release must be an .exe.", "NOT_AN_EXE"));

        var target = Path.Combine(ReleaseFolder, safeName);

        var staging = target + ".part";
        await using (var stream = System.IO.File.Create(staging))
        {
            await file.CopyToAsync(stream, ct);
        }

        string hash;
        await using (var stream = System.IO.File.OpenRead(staging))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        }

        System.IO.File.Move(staging, target, overwrite: true);

        record.AgentFileName = safeName;
        record.AgentSha256 = hash;
        record.AgentSizeBytes = file.Length;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Published agent exe {FileName} ({SizeMb:N0} MB) for version {Version}. SHA-256 {Hash}.",
            safeName, file.Length / 1024d / 1024d, version, hash);

        return Ok(ApiResponse<object>.Ok(new
        {
            version = record.CurrentVersion,
            fileName = safeName,
            sha256 = hash,
            sizeBytes = file.Length,
            approved = record.ApprovedForRollout,
        }));
    }
}
