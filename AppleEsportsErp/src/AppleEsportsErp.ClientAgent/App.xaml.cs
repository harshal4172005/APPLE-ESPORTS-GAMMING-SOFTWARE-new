using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;

namespace AppleEsportsErp.ClientAgent;

/// <summary>
/// Single-instance WPF application entry point for the Gaming PC Agent.
/// Reads configuration and launches the lock screen.
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    public static IConfiguration Configuration { get; private set; } = null!;
    public static AgentConfig AgentConfig { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enforce single instance — prevent double-launching
        const string mutexName = "AppleEsportsErp_ClientAgent_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("Apple Esports Agent is already running.", "Agent", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Load configuration
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.agent.json");
        Configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false, reloadOnChange: true)
            .Build();

        AgentConfig = new AgentConfig();
        Configuration.GetSection("Agent").Bind(AgentConfig);

        // Launch the appropriate window based on role
        if (string.IsNullOrEmpty(AgentConfig.AssignedRole))
        {
            var dashboardWindow = new Views.DashboardWindow("");
            dashboardWindow.Show();
        }
        else if (AgentConfig.AssignedRole == "Operator" || 
                 AgentConfig.AssignedRole == "Admin" || 
                 AgentConfig.AssignedRole == "SuperAdmin")
        {
            var dashboardWindow = new Views.DashboardWindow(AgentConfig.AssignedRole);
            dashboardWindow.Show();
        }
        else // Customer or anything else
        {
            var lockScreen = new Views.LockScreen();
            lockScreen.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

/// <summary>Configuration model for the agent</summary>
public class AgentConfig
{
    public string AssignedRole { get; set; } = "";
    public string PcId { get; set; } = "";
    public string BranchId { get; set; } = "";
    public string PcNumber { get; set; } = "PC-1";
    public string MachineToken { get; set; } = "";
    public string OperatorLanUrl { get; set; } = "http://localhost:5000";
    public string FrontendUrl { get; set; } = "http://localhost:5173";
    public string CloudUrl { get; set; } = "https://api.appleesports.com";
    public int HealthCheckIntervalSeconds { get; set; } = 10;
    public int FailoverThresholdSeconds { get; set; } = 30;
}
