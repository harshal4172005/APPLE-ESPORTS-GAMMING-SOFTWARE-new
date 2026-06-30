using System.Diagnostics;
using System.Text.Json;

namespace AppleEsportsErp.ClientAgent.Services;

/// <summary>
/// Handles incoming commands (Unlock, Lock, Shutdown) from the SignalR hub.
/// Delegates UI updates to the LockScreen.
/// </summary>
public class SessionControlService
{
    private readonly Views.LockScreen _lockScreen;

    public SessionControlService(Views.LockScreen lockScreen)
    {
        _lockScreen = lockScreen;
    }

    /// <summary>Handle an unlock command from Operator or Admin</summary>
    public void HandleUnlock(object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var command = JsonSerializer.Deserialize<UnlockCommand>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            var duration = command?.DurationMinutes ?? 60;
            var customerName = command?.CustomerName;

            _lockScreen.UnlockPc(duration, customerName);
        }
        catch
        {
            // Fallback — unlock with default 60 minutes
            _lockScreen.UnlockPc(60, null);
        }
    }

    /// <summary>Handle a lock command from Operator or Admin</summary>
    public void HandleLock()
    {
        _lockScreen.LockPc();
    }

    /// <summary>Handle a force shutdown command from Admin</summary>
    public void HandleShutdown()
    {
        // First lock the screen
        _lockScreen.LockPc();

        // Then initiate Windows shutdown
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/s /t 10 /c \"Apple Esports: Remote shutdown initiated by Admin.\"",
                UseShellExecute = true,
                CreateNoWindow = true
            });
        }
        catch
        {
            // If shutdown fails, at least the screen is locked
        }
    }
}

internal class UnlockCommand
{
    public int DurationMinutes { get; set; }
    public string? CustomerName { get; set; }
}
