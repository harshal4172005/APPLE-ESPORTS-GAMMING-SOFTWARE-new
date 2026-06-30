using Microsoft.AspNetCore.SignalR.Client;

namespace AppleEsportsErp.ClientAgent.Services;

/// <summary>
/// The brain of the dual-connection failover system.
/// Maintains TWO SignalR connections (LAN primary, Cloud fallback).
/// Automatically switches between them based on health checks.
/// </summary>
public class DualConnectionService
{
    private readonly Views.LockScreen _lockScreen;
    private readonly SessionControlService _sessionControl;
    private readonly AgentConfig _config;

    private HubConnection? _lanConnection;
    private HubConnection? _cloudConnection;
    private HubConnection? _activeConnection;

    private string _currentMode = "None";
    private int _lanFailCount = 0;
    private bool _isRunning = false;
    private CancellationTokenSource _cts = new();

    public string CurrentMode => _currentMode;

    public DualConnectionService(Views.LockScreen lockScreen, SessionControlService sessionControl)
    {
        _lockScreen = lockScreen;
        _sessionControl = sessionControl;
        _config = App.AgentConfig;
    }

    /// <summary>Start the dual connection system</summary>
    public async Task StartAsync()
    {
        _isRunning = true;

        // Build both connections
        _lanConnection = BuildConnection(_config.OperatorLanUrl);
        _cloudConnection = BuildConnection(_config.CloudUrl);

        // Register command handlers on BOTH connections
        RegisterHandlers(_lanConnection);
        RegisterHandlers(_cloudConnection);

        // Try LAN first (primary)
        if (await TryConnect(_lanConnection, "LAN"))
        {
            SetActiveConnection(_lanConnection, "LAN");
        }
        // Fall back to Cloud
        else if (await TryConnect(_cloudConnection, "Cloud"))
        {
            SetActiveConnection(_cloudConnection, "Cloud");
        }
        else
        {
            _lockScreen.UpdateConnectionStatus("None", false);
        }

        // Start the health check loop
        _ = Task.Run(() => HealthCheckLoop(_cts.Token));
    }

    /// <summary>Notify the backend that a session timer expired (auto-lock)</summary>
    public async Task NotifySessionExpired()
    {
        if (_activeConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _activeConnection.InvokeAsync("AgentModeChanged", _config.PcId, "SessionExpired");
            }
            catch { /* Swallow — non-critical */ }
        }
    }

    private HubConnection BuildConnection(string baseUrl)
    {
        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/pc-status?access_token={_config.MachineToken}";
        
        return new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();
    }

    private void RegisterHandlers(HubConnection connection)
    {
        // Listen for unlock commands from Operator or Admin
        connection.On<object>("UnlockSession", (data) =>
        {
            _sessionControl.HandleUnlock(data);
        });

        // Listen for lock commands
        connection.On<object>("LockSession", (data) =>
        {
            _sessionControl.HandleLock();
        });

        // Listen for shutdown commands (Admin only)
        connection.On<object>("ForceShutdown", (data) =>
        {
            _sessionControl.HandleShutdown();
        });

        connection.Closed += async (error) =>
        {
            await Task.Delay(2000);
            // Reconnection is handled by WithAutomaticReconnect
        };
    }

    private async Task<bool> TryConnect(HubConnection connection, string mode)
    {
        try
        {
            await connection.StartAsync();
            
            // Announce ourselves to the hub
            await connection.InvokeAsync("AgentConnected", _config.PcId, mode);
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SetActiveConnection(HubConnection connection, string mode)
    {
        _activeConnection = connection;
        _currentMode = mode;
        _lockScreen.UpdateConnectionStatus(mode, true);
    }

    /// <summary>Continuous health check loop — monitors LAN connection and triggers failover</summary>
    private async Task HealthCheckLoop(CancellationToken ct)
    {
        var healthCheckInterval = TimeSpan.FromSeconds(_config.HealthCheckIntervalSeconds);
        var failoverThreshold = _config.FailoverThresholdSeconds / _config.HealthCheckIntervalSeconds;

        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(healthCheckInterval, ct);

                // Check LAN connection health
                if (_lanConnection?.State == HubConnectionState.Connected)
                {
                    _lanFailCount = 0;

                    // If we're currently on Cloud but LAN is back, switch to LAN
                    if (_currentMode == "Cloud")
                    {
                        SetActiveConnection(_lanConnection, "LAN");
                        
                        // Notify the cloud that we're back on LAN
                        if (_cloudConnection?.State == HubConnectionState.Connected)
                        {
                            try { await _cloudConnection.InvokeAsync("AgentModeChanged", _config.PcId, "LAN"); }
                            catch { }
                        }
                    }

                    // Send heartbeat
                    try { await _lanConnection.InvokeAsync("AgentHeartbeat", _config.PcId, "LAN"); }
                    catch { _lanFailCount++; }
                }
                else
                {
                    _lanFailCount++;

                    // Try to reconnect LAN
                    if (_lanConnection?.State == HubConnectionState.Disconnected)
                    {
                        _ = TryConnect(_lanConnection, "LAN");
                    }

                    // Failover: switch to Cloud after threshold
                    if (_lanFailCount >= failoverThreshold && _currentMode != "Cloud")
                    {
                        // Ensure cloud is connected
                        if (_cloudConnection?.State != HubConnectionState.Connected)
                        {
                            await TryConnect(_cloudConnection!, "Cloud");
                        }

                        if (_cloudConnection?.State == HubConnectionState.Connected)
                        {
                            SetActiveConnection(_cloudConnection, "Cloud");
                            await _cloudConnection.InvokeAsync("AgentModeChanged", _config.PcId, "Cloud");
                        }
                        else
                        {
                            _lockScreen.UpdateConnectionStatus("None", false);
                        }
                    }
                }

                // Also keep cloud connection alive (passive heartbeat)
                if (_cloudConnection?.State == HubConnectionState.Connected)
                {
                    try { await _cloudConnection.InvokeAsync("AgentHeartbeat", _config.PcId, _currentMode); }
                    catch { }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* Swallow unexpected errors in health check loop */ }
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _cts.Cancel();

        if (_lanConnection != null) await _lanConnection.DisposeAsync();
        if (_cloudConnection != null) await _cloudConnection.DisposeAsync();
    }
}
