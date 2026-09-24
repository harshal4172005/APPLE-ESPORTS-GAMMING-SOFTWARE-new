namespace AppleEsportsErp.Application.DTOs;

public class VersionInfoDto
{
    public int Id { get; set; }
    public string CurrentVersion { get; set; }
    public string ReleaseNotes { get; set; }
    public bool ApprovedForRollout { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string ApprovedByUserId { get; set; }
    public int BranchesApprovedCount { get; set; }

    /// <summary>
    /// Whether there is actually something for a branch to download and run.
    ///
    /// The page uses this to decide whether to offer "Update Now" at all. Without it the button
    /// would sit there on an approved version that has no installer behind it yet, and pressing
    /// it would appear to do nothing — which reads as a broken dashboard rather than as a part
    /// of the system that is not finished.
    /// </summary>
    public bool HasInstaller { get; set; }

    /// <summary>
    /// Whether the standalone gaming-PC agent exe has been published against this version too -
    /// the same gate as <see cref="HasInstaller"/>, for the artifact AgentSelfUpdater fetches
    /// instead of the branch installer. A version can have one without the other: a pure
    /// server-side fix has nothing for a gaming PC to take.
    /// </summary>
    public bool HasAgent { get; set; }
}

public class BranchVersionStatusDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; }
    public string CurrentVersion { get; set; }
    public string LatestApprovedVersion { get; set; }
    public bool AutoUpdateEnabled { get; set; }
    public bool UpdateAvailable { get; set; }
    public DateTime LastCheckedForUpdates { get; set; }
    public DateTime LastUpdated { get; set; }
    public int GamingPcsUpToDateCount { get; set; }
    public int GamingPcsTotalCount { get; set; }

    // How an update is going right now, straight from the branch. Null stage = nothing running.
    public string? UpdateStage { get; set; }
    public int UpdateProgressPercent { get; set; }
    public string? UpdateMessage { get; set; }
    public DateTime? UpdateStageChangedAt { get; set; }
}

public class ApproveUpdateDto
{
    public int VersionInfoId { get; set; }
}

public class UpdateBranchAutoUpdateDto
{
    public Guid BranchId { get; set; }
    public bool AutoUpdateEnabled { get; set; }
}
