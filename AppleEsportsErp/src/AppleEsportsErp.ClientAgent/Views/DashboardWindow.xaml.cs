using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AppleEsportsErp.ClientAgent.Views;

public partial class DashboardWindow : Window
{
    private readonly string _role;

    public DashboardWindow(string role)
    {
        _role = role;
        InitializeComponent();
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        // Initialize WebView2 environment
        var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppleEsportsAgent", "WebView2");
        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await DashboardWebView.EnsureCoreWebView2Async(env);
        
        DashboardWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;

        // Determine the frontend route based on role
        string route = "/";
        if (_role == "Operator") route = "/login/operator";
        else if (_role == "Admin") route = "/login/admin";
        else if (_role == "SuperAdmin") route = "/login/superadmin";

        // Navigate to the Frontend URL
        var baseUrl = App.AgentConfig.FrontendUrl.TrimEnd('/');
        DashboardWebView.Source = new Uri(baseUrl + route);
    }

    private async void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = e.Uri.ToLower();

        // Intercept Customer/User selection
        if (uri.Contains("/user/select"))
        {
            e.Cancel = true; // Stop web navigation
            await SaveRoleAndLock("Customer");
        }
        // Intercept Staff selections to save their role so it remembers next time
        else if (uri.Contains("/login/operator") && string.IsNullOrEmpty(App.AgentConfig.AssignedRole))
        {
            await SaveRoleOnly("Operator");
        }
        else if (uri.Contains("/login/admin") && string.IsNullOrEmpty(App.AgentConfig.AssignedRole))
        {
            await SaveRoleOnly("Admin");
        }
        else if (uri.Contains("/login/superadmin") && string.IsNullOrEmpty(App.AgentConfig.AssignedRole))
        {
            await SaveRoleOnly("SuperAdmin");
        }
    }

    private async Task SaveRoleAndLock(string role)
    {
        await SaveRoleOnly(role);
        
        // Switch to the native padlock
        var lockScreen = new LockScreen();
        lockScreen.Show();
        this.Close();
    }

    private async Task SaveRoleOnly(string role)
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.agent.json");
            var jsonString = await File.ReadAllTextAsync(configPath);
            var jsonNode = JsonNode.Parse(jsonString);

            if (jsonNode?["Agent"] is JsonObject agentObj)
            {
                agentObj["AssignedRole"] = role;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(configPath, jsonNode?.ToJsonString(options));

            App.AgentConfig.AssignedRole = role;
        }
        catch (Exception) { /* Handle silently for now */ }
    }
}
