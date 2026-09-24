using System.Diagnostics;

namespace AppleEsportsErp.Api;

/// <summary>
/// Makes sure Windows Firewall actually lets gaming PCs reach this branch's API, checked on
/// every startup, the same way AutoUpdateTaskGuard repairs the update task and DbUpdater
/// repairs the schema.
///
/// The API is bound to every network interface (0.0.0.0) specifically so a gaming PC on the
/// branch LAN can reach it - but binding to an interface and being reachable on it are two
/// different things. Windows Firewall blocks an unsolicited inbound connection to this service
/// by default, and there is no desktop session here for the usual "allow this app through the
/// firewall?" prompt a foreground program gets, so nothing ever asks and nothing ever opens it.
/// A gaming PC's own setup wizard then times out testing the branch's LAN address - "the server
/// did not answer in time" - with nothing anywhere to say a firewall, not the network or the
/// address, is what actually refused it.
///
/// The installer now opens this rule too (see setup-api.ps1), which is enough for a brand new
/// branch. It is not enough for one already running: an existing install only ever gets new
/// binaries from an update, never a re-run of the installer's setup steps, so a branch already
/// live before this fix existed would carry the same closed port forever, through any number of
/// future updates, unless something checks again on its own. That is what this does - the exact
/// same reasoning AutoUpdateTaskGuard was written on.
/// </summary>
public static class FirewallGuard
{
    private const string RuleName = "Apple Esports Branch API";
    private const int ApiPort = 5016;

    /// <summary>
    /// Never throws: a branch that cannot repair its own firewall rule must still serve
    /// customers at the counter, and an unhandled error here must not be the reason the API
    /// fails to start.
    /// </summary>
    public static void EnsureOpen(ILogger logger)
    {
        // A no-op away from a real branch: Head Office runs in Linux containers, where netsh
        // does not exist and nothing outside its own container ever needs to reach it directly.
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            if (RuleExists())
            {
                logger.LogInformation("Firewall rule for port {Port} already present ✓", ApiPort);
                return;
            }

            using var p = Process.Start(new ProcessStartInfo("netsh.exe",
                $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow " +
                $"protocol=TCP localport={ApiPort} profile=any")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            p?.WaitForExit(15000);

            if (p != null && p.ExitCode == 0)
                logger.LogInformation("Opened port {Port} in Windows Firewall for gaming PCs on the branch LAN ✓", ApiPort);
            else
                logger.LogWarning("Could not open the firewall rule for port {Port} (netsh exit {Code})", ApiPort, p?.ExitCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Firewall rule check failed - continuing without it");
        }
    }

    /// <summary>
    /// True only when a rule by this name genuinely exists. netsh's exit code alone is not
    /// trustworthy for this across Windows versions - some report success even when nothing
    /// matched - so this reads the actual output instead of trusting the process's exit code.
    /// </summary>
    private static bool RuleExists()
    {
        using var p = Process.Start(new ProcessStartInfo("netsh.exe",
            $"advfirewall firewall show rule name=\"{RuleName}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        if (p is null) return false;

        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(15000);

        return !output.Contains("No rules match", StringComparison.OrdinalIgnoreCase);
    }
}
