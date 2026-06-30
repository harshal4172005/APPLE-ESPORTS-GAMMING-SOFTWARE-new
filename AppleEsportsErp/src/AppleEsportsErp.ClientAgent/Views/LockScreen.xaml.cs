using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

using System.Windows.Input;

namespace AppleEsportsErp.ClientAgent.Views;

/// <summary>
/// Lock Screen code-behind — manages UI state, timer display, and connection status.
/// The actual session control and dual-connection logic lives in the Services.
/// </summary>
public partial class LockScreen : Window
{
    private readonly DispatcherTimer _sessionTimer;
    private readonly Services.SystemLockService _systemLock;
    private readonly Services.DualConnectionService _dualConnection;
    private readonly Services.SessionControlService _sessionControl;
    private int _remainingSeconds = 0;

    public LockScreen()
    {
        InitializeComponent();

        // Set PC number from config
        PcNumberText.Text = App.AgentConfig.PcNumber;

        // Start glow animation
        var storyboard = (Storyboard)FindResource("PulseAnimation");
        storyboard.Begin();

        // Initialize services
        _systemLock = new Services.SystemLockService();
        _sessionControl = new Services.SessionControlService(this);
        _dualConnection = new Services.DualConnectionService(this, _sessionControl);

        // Session countdown timer
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _sessionTimer.Tick += SessionTimer_Tick;

        // Lock the system on startup
        _systemLock.EnableLock();

        // Start dual connection
        _ = _dualConnection.StartAsync();

        // Prevent closing via Alt+F4
        Closing += (s, e) => e.Cancel = true;

        // Secret escape hatch for developers/emergency (Ctrl+Shift+Alt+U)
        KeyDown += LockScreen_KeyDown;
    }

    private void LockScreen_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.U && 
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && 
            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && 
            Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            _systemLock.DisableLock();
            Application.Current.Shutdown();
        }
    }

    /// <summary>Called by SessionControlService when an unlock command is received</summary>
    public void UnlockPc(int durationMinutes, string? customerName)
    {
        Dispatcher.Invoke(() =>
        {
            _systemLock.DisableLock();

            if (durationMinutes > 0)
            {
                _remainingSeconds = durationMinutes * 60;
                TimerText.Visibility = Visibility.Visible;
                _sessionTimer.Start();
            }

            // Hide the lock screen (don't close — we need to show it again later)
            Hide();
        });
    }

    /// <summary>Called by SessionControlService when a lock command is received</summary>
    public void LockPc()
    {
        Dispatcher.Invoke(() =>
        {
            _sessionTimer.Stop();
            TimerText.Visibility = Visibility.Collapsed;
            _remainingSeconds = 0;

            _systemLock.EnableLock();
            Show();
            Activate();
            Topmost = true;
        });
    }

    /// <summary>Update the connection status indicator</summary>
    public void UpdateConnectionStatus(string mode, bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            if (isConnected)
            {
                StatusDot.Fill = mode == "LAN" 
                    ? new SolidColorBrush(Color.FromRgb(0, 255, 136))   // Green for LAN
                    : new SolidColorBrush(Color.FromRgb(255, 165, 0));  // Orange for Cloud
                StatusText.Text = mode == "LAN" 
                    ? "Connected — LAN Mode" 
                    : "Connected — ☁️ Cloud Mode (Operator offline)";
            }
            else
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 50, 50)); // Red
                StatusText.Text = "Disconnected — Attempting to reconnect...";
            }
        });
    }

    private void SessionTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;

        if (_remainingSeconds <= 0)
        {
            // Time's up — auto-lock
            _sessionTimer.Stop();
            LockPc();
            _ = _dualConnection.NotifySessionExpired();
            return;
        }

        // Update timer display
        var hours = _remainingSeconds / 3600;
        var minutes = (_remainingSeconds % 3600) / 60;
        var seconds = _remainingSeconds % 60;

        TimerText.Text = hours > 0 
            ? $"{hours:D2}:{minutes:D2}:{seconds:D2}" 
            : $"{minutes:D2}:{seconds:D2}";

        // Flash timer red when less than 5 minutes remain
        if (_remainingSeconds <= 300)
        {
            TimerText.Foreground = _remainingSeconds % 2 == 0
                ? new SolidColorBrush(Color.FromRgb(255, 50, 50))
                : new SolidColorBrush(Color.FromRgb(255, 215, 0));
        }
    }
}
