namespace AppleEsportsErp.Domain.Entities;

public class VersionInfo
{
    public int Id { get; set; }
    public string CurrentVersion { get; set; }
    public string ReleaseNotes { get; set; }
    public bool ApprovedForRollout { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    // Nullable alongside ApprovedAt, deliberately: CreateVersionAsync makes a row with neither
    // set, and ApproveVersionAsync is the only place that ever fills either in. The column was
    // NOT NULL with no default until now, which meant creating a version - the first of the two
    // steps in the API this app actually exposes - could never succeed on its own. Every
    // version before this one was ever only written by a single direct SQL insert that set both
    // fields together in the same statement, which is the only reason this was never hit.
    public string? ApprovedByUserId { get; set; }
    public int BranchesApprovedCount { get; set; }

    // ── The installer this version refers to ──

    /// <summary>
    /// File name of the installer held by Head Office, e.g. "AppleEsports-Setup-2.1.0.exe".
    /// A name rather than a full URL, so branches always fetch from the Head Office they are
    /// already talking to and a stale row can never point them somewhere else.
    /// </summary>
    public string? InstallerFileName { get; set; }

    /// <summary>
    /// SHA-256 of that installer, checked by the branch before it runs anything.
    ///
    /// This is the part that matters. A branch downloads an executable and runs it with full
    /// privileges; without verifying it, anyone able to interfere with the connection could
    /// hand all four branches a program of their choosing. Branches talk to Head Office over
    /// plain HTTP today, so this hash is currently the only thing standing in the way.
    /// An update with no hash recorded is refused rather than trusted.
    /// </summary>
    public string? InstallerSha256 { get; set; }

    public long InstallerSizeBytes { get; set; }

    // ── The gaming-PC agent artifact this version refers to ──
    //
    // A separate file from the installer above, deliberately. The branch installer is ~165MB and
    // needs an elevated run to replace Program Files and Windows services; a gaming PC only ever
    // needs to replace its own single exe, and nothing on a gaming PC is elevated once it is
    // installed (see AppleEsportsAgent's AgentSelfUpdater and AppleEsportsBranch.iss's
    // Permissions: users-modify on {app}\agent). Publishing the two artifacts separately is what
    // lets a gaming PC update itself without ever downloading the installer or asking for
    // elevation again.

    /// <summary>File name of the standalone agent exe held by Head Office, e.g. "AppleEsportsAgent-3.1.23.exe".</summary>
    public string? AgentFileName { get; set; }

    /// <summary>SHA-256 of that exe, checked by the agent itself before it ever overwrites its own file.</summary>
    public string? AgentSha256 { get; set; }

    public long AgentSizeBytes { get; set; }
}
