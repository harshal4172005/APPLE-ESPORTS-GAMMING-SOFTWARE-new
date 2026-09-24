using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;

namespace AppleEsportsErp.Infrastructure.Services;

public class VersionService : IVersionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminNotifier _notifier;

    public VersionService(IUnitOfWork unitOfWork, IAdminNotifier notifier)
    {
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task<VersionInfoDto?> GetLatestVersionAsync()
    {
        var version = await _unitOfWork.Repository<VersionInfo>()
            .Query()
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();

        if (version == null)
            return null;

        return MapToVersionInfoDto(version);
    }

    public async Task<List<BranchVersionStatusDto>> GetAllBranchVersionStatusesAsync()
    {
        var statuses = await _unitOfWork.Repository<BranchVersionStatus>()
            .Query()
            .Include(s => s.Branch)
            .ToListAsync();

        var latestVersion = await GetLatestVersionAsync();
        var result = new List<BranchVersionStatusDto>();

        foreach (var status in statuses)
        {
            var dto = MapToBranchVersionStatusDto(status);
            if (latestVersion != null)
            {
                dto.LatestApprovedVersion = latestVersion.ApprovedForRollout ? latestVersion.CurrentVersion : null;
                dto.UpdateAvailable = latestVersion.ApprovedForRollout && !status.CurrentVersion.Equals(latestVersion.CurrentVersion);
            }
            result.Add(dto);
        }

        return result;
    }

    public async Task<BranchVersionStatusDto?> GetBranchVersionStatusAsync(Guid branchId)
    {
        var status = await _unitOfWork.Repository<BranchVersionStatus>()
            .Query()
            .Where(s => s.BranchId == branchId)
            .FirstOrDefaultAsync();

        if (status == null)
            return null;

        var dto = MapToBranchVersionStatusDto(status);
        var latestVersion = await GetLatestVersionAsync();

        if (latestVersion != null)
        {
            dto.LatestApprovedVersion = latestVersion.ApprovedForRollout ? latestVersion.CurrentVersion : null;
            dto.UpdateAvailable = latestVersion.ApprovedForRollout && !status.CurrentVersion.Equals(latestVersion.CurrentVersion);
        }

        return dto;
    }

    /// <summary>
    /// Every version ever published, newest first.
    ///
    /// Kept permanently and never trimmed. An operator who wants to know what changed - or the
    /// owner asking "when did that behaviour arrive?" - has nowhere else to look; the answer
    /// would otherwise only exist in a commit log nobody at a branch can read.
    /// </summary>
    public async Task<List<VersionInfoDto>> GetVersionHistoryAsync()
    {
        var versions = await _unitOfWork.Repository<VersionInfo>()
            .Query()
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

        return versions.Select(MapToVersionInfoDto).ToList();
    }

    public async Task<VersionInfoDto> CreateVersionAsync(string version, string releaseNotes)
    {
        var versionInfo = new VersionInfo
        {
            CurrentVersion = version,
            ReleaseNotes = releaseNotes,
            ApprovedForRollout = false,
            CreatedAt = DateTime.UtcNow,
            BranchesApprovedCount = 0
        };

        await _unitOfWork.Repository<VersionInfo>().AddAsync(versionInfo);
        await _unitOfWork.CommitTransactionAsync();

        return MapToVersionInfoDto(versionInfo);
    }

    public async Task<VersionInfoDto> ApproveVersionAsync(int versionInfoId, string userId)
    {
        var version = await _unitOfWork.Repository<VersionInfo>()
            .Query()
            .FirstOrDefaultAsync(v => v.Id == versionInfoId);

        if (version == null)
            throw new Exception($"Version {versionInfoId} not found");

        // Approving twice would send the branches a second "an update is waiting" email about
        // an update they already have. Harmless to the data, but it teaches people to ignore
        // the emails, which is the one thing this mechanism cannot afford.
        if (version.ApprovedForRollout)
            return MapToVersionInfoDto(version);

        version.ApprovedForRollout = true;
        version.ApprovedAt = DateTime.UtcNow;
        version.ApprovedByUserId = userId;

        _unitOfWork.Repository<VersionInfo>().Update(version);
        await _unitOfWork.CommitTransactionAsync();

        // After the commit, on purpose. If the email were sent first and the save then failed,
        // every branch would have been told about an update the system does not consider
        // approved. Told-late is recoverable; told-wrongly is not.
        await NotifyBranchesOfApprovedUpdateAsync(version);

        return MapToVersionInfoDto(version);
    }

    /// <summary>
    /// Takes a version off the branches without deleting its record - the release notes and
    /// installer both stay, so it can be approved again later without re-uploading anything.
    /// This is the soft version of DeleteVersionAsync, for when it might still be wanted.
    ///
    /// Exists because approval had no way back. A version approved by mistake - or approved
    /// again after being deliberately rolled back, which is exactly what happened the one time
    /// this was needed - stayed the newest approved version forever, with nothing on this
    /// screen able to undo it short of editing the database directly.
    /// </summary>
    public async Task<VersionInfoDto> UnapproveVersionAsync(int versionInfoId)
    {
        var version = await _unitOfWork.Repository<VersionInfo>()
            .Query()
            .FirstOrDefaultAsync(v => v.Id == versionInfoId);

        if (version == null)
            throw new Exception($"Version {versionInfoId} not found");

        version.ApprovedForRollout = false;

        _unitOfWork.Repository<VersionInfo>().Update(version);
        await _unitOfWork.CommitTransactionAsync();

        return MapToVersionInfoDto(version);
    }

    /// <summary>
    /// Removes a version's record outright, approved or not - what "this should never have
    /// existed" actually needs, rather than merely hiding it. GetLatestVersionAsync takes
    /// whichever record is newest by creation date with no regard for approval, so a version
    /// rolled back by UnapproveVersionAsync alone still shows as "Newest update" on this same
    /// screen; deleting it here is what actually lets the previous version take that place
    /// again.
    ///
    /// Only removes the database record. The installer and agent files themselves are left for
    /// the caller to delete - this layer has no notion of where releases are stored on disk, and
    /// should not need one just to remove a row.
    /// </summary>
    public async Task<(string? InstallerFileName, string? AgentFileName)> DeleteVersionAsync(int versionInfoId)
    {
        var version = await _unitOfWork.Repository<VersionInfo>()
            .Query()
            .FirstOrDefaultAsync(v => v.Id == versionInfoId);

        if (version == null)
            throw new Exception($"Version {versionInfoId} not found");

        var fileNames = (version.InstallerFileName, version.AgentFileName);

        _unitOfWork.Repository<VersionInfo>().Remove(version);
        await _unitOfWork.CommitTransactionAsync();

        return fileNames;
    }

    /// <summary>
    /// Tells the branches an update is waiting. Plain English throughout - the person reading
    /// this is behind a counter, not at a keyboard, and "artefact", "rollout" and "deployment"
    /// mean nothing to them.
    /// </summary>
    private async Task NotifyBranchesOfApprovedUpdateAsync(VersionInfo version)
    {
        var notes = string.IsNullOrWhiteSpace(version.ReleaseNotes)
            ? "No details were written for this update."
            : version.ReleaseNotes.Trim();

        var rows = new List<(string Label, string Value)>
        {
            ("New version", version.CurrentVersion),
            ("", ""),
            ("What is in it", notes),
        };

        var body = AdminEmailTemplate.Compose(
            heading: "A new update is ready for your branch",
            accent: AdminEmailTemplate.Green,
            summary: "The owner has approved a new version of the Apple Esports software. " +
                     "If your branch is set to update by itself, it will install on its own and " +
                     "you do not need to do anything. If not, open the Updates page and press " +
                     "Update Now when the shop is quiet.",
            rows: rows,
            headline: "Version " + version.CurrentVersion,
            footnote: "Nothing will interrupt a customer who is playing. You can read the full " +
                      "list of what changed on the Updates page in your dashboard.");

        await _notifier.NotifyOperatorsAsync(
            $"Apple Esports update {version.CurrentVersion} is ready for your branch", body);
    }

    public async Task UpdateBranchAutoUpdateAsync(Guid branchId, bool autoUpdateEnabled)
    {
        var status = await _unitOfWork.Repository<BranchVersionStatus>()
            .Query()
            .FirstOrDefaultAsync(s => s.BranchId == branchId);

        if (status == null)
        {
            status = new BranchVersionStatus
            {
                BranchId = branchId,
                CurrentVersion = "1.0.0",
                AutoUpdateEnabled = autoUpdateEnabled,
                LastCheckedForUpdates = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                GamingPcsUpToDateCount = 0,
                GamingPcsTotalCount = 0
            };
            await _unitOfWork.Repository<BranchVersionStatus>().AddAsync(status);
        }
        else
        {
            status.AutoUpdateEnabled = autoUpdateEnabled;
            status.LastCheckedForUpdates = DateTime.UtcNow;
            _unitOfWork.Repository<BranchVersionStatus>().Update(status);
        }

        await _unitOfWork.CommitTransactionAsync();
    }

    public async Task UpdateBranchVersionStatusAsync(Guid branchId, string currentVersion, int upToDateCount, int totalCount)
    {
        var status = await _unitOfWork.Repository<BranchVersionStatus>()
            .Query()
            .FirstOrDefaultAsync(s => s.BranchId == branchId);

        if (status == null)
        {
            // The version Head Office would like this branch to be on. The column does not
            // accept null, and this line never set it - so the very first report from any
            // branch failed with a not-null violation, the endpoint answered 500, and the
            // branch retried every fifteen minutes for ever.
            //
            // It was invisible for as long as it was untrue: no branch had ever reported, so
            // the row-creation path had never once run. The first branch to report found it
            // immediately, and the Updates page stayed empty while the branch log filled with
            // refusals nobody was reading.
            var latestApproved = await _unitOfWork.Repository<VersionInfo>()
                .Query()
                .Where(v => v.ApprovedForRollout)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => v.CurrentVersion)
                .FirstOrDefaultAsync();

            status = new BranchVersionStatus
            {
                BranchId = branchId,
                CurrentVersion = currentVersion,
                // Falls back to what the branch says it is running. "Nothing has been approved
                // yet" and "this branch is behind" are different things, and an empty string
                // here would read as the second.
                LatestApprovedVersion = latestApproved ?? currentVersion,
                // AutoUpdateEnabled is deliberately not set here. The entity defaults it to
                // ON, and this is the line a branch takes the very first time it reports in -
                // setting it false here switched automatic updates off for every branch at
                // the moment it appeared, which is exactly the opposite of the default.
                LastCheckedForUpdates = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                GamingPcsUpToDateCount = upToDateCount,
                GamingPcsTotalCount = totalCount
            };
            await _unitOfWork.Repository<BranchVersionStatus>().AddAsync(status);
        }
        else
        {
            status.CurrentVersion = currentVersion;
            status.LastCheckedForUpdates = DateTime.UtcNow;
            status.LastUpdated = DateTime.UtcNow;
            status.GamingPcsUpToDateCount = upToDateCount;
            status.GamingPcsTotalCount = totalCount;
            _unitOfWork.Repository<BranchVersionStatus>().Update(status);
        }

        await _unitOfWork.CommitTransactionAsync();
    }

    public async Task ReportUpdateProgressAsync(Guid branchId, string stage, int progressPercent, string? message)
    {
        var status = await _unitOfWork.Repository<BranchVersionStatus>()
            .Query()
            .FirstOrDefaultAsync(s => s.BranchId == branchId);

        // A branch reporting progress before it has ever reported a version would be odd, but
        // dropping the report would leave the operator watching a bar that never moves.
        if (status == null)
        {
            status = new BranchVersionStatus { BranchId = branchId, CurrentVersion = "unknown" };
            await _unitOfWork.Repository<BranchVersionStatus>().AddAsync(status);
        }

        var normalised = (stage ?? string.Empty).Trim().ToLowerInvariant();

        // Only the stage changing counts as a change of stage. Percent moving within a download
        // is not, or the "nothing has happened for twenty minutes" check could never fire.
        var stageChanged = status.UpdateStage != normalised;
        if (stageChanged)
            status.UpdateStageChangedAt = DateTime.UtcNow;

        status.UpdateStage = normalised;
        status.UpdateProgressPercent = Math.Clamp(progressPercent, 0, 100);

        // No message means "nothing new to say", not "forget what I told you". The branch sends
        // the explanation once when the stage turns over and then reports bare percentages as the
        // download moves - a few dozen of them - so assigning null every time would have wiped
        // "Downloading version 3.0.8" on the very next tick and left the operator a moving bar
        // with no words next to it. A stage change with no message does clear it, because a
        // sentence about downloading must not survive into installing.
        if (message is not null)
            status.UpdateMessage = message;
        else if (stageChanged)
            status.UpdateMessage = null;

        if (normalised == "done")
        {
            status.UpdateProgressPercent = 100;
            status.LastUpdated = DateTime.UtcNow;
        }

        _unitOfWork.Repository<BranchVersionStatus>().Update(status);
        await _unitOfWork.CommitTransactionAsync();
    }

    private VersionInfoDto MapToVersionInfoDto(VersionInfo version)
    {
        return new VersionInfoDto
        {
            Id = version.Id,
            CurrentVersion = version.CurrentVersion,
            ReleaseNotes = version.ReleaseNotes,
            ApprovedForRollout = version.ApprovedForRollout,
            CreatedAt = version.CreatedAt,
            ApprovedAt = version.ApprovedAt,
            ApprovedByUserId = version.ApprovedByUserId,
            BranchesApprovedCount = version.BranchesApprovedCount,

            // Both, not just the file name. A recorded installer with no hash is refused by the
            // branch anyway, so offering Update Now for one would produce a failure rather than
            // an update.
            HasInstaller = !string.IsNullOrWhiteSpace(version.InstallerFileName)
                        && !string.IsNullOrWhiteSpace(version.InstallerSha256),

            HasAgent = !string.IsNullOrWhiteSpace(version.AgentFileName)
                    && !string.IsNullOrWhiteSpace(version.AgentSha256)
        };
    }

    private BranchVersionStatusDto MapToBranchVersionStatusDto(BranchVersionStatus status)
    {
        return new BranchVersionStatusDto
        {
            Id = status.Id,
            BranchId = status.BranchId,
            BranchName = status.Branch?.Name ?? $"Branch {status.BranchId}",
            CurrentVersion = status.CurrentVersion,
            LatestApprovedVersion = status.LatestApprovedVersion,
            AutoUpdateEnabled = status.AutoUpdateEnabled,
            LastCheckedForUpdates = status.LastCheckedForUpdates,
            LastUpdated = status.LastUpdated,
            GamingPcsUpToDateCount = status.GamingPcsUpToDateCount,
            GamingPcsTotalCount = status.GamingPcsTotalCount,
            UpdateStage = status.UpdateStage,
            UpdateProgressPercent = status.UpdateProgressPercent,
            UpdateMessage = status.UpdateMessage,
            UpdateStageChangedAt = status.UpdateStageChangedAt
        };
    }
}
