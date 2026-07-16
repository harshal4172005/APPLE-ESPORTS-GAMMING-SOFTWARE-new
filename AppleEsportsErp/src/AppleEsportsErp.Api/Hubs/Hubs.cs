using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AppleEsportsErp.Application.Constants;

namespace AppleEsportsErp.Api.Hubs;

/// <summary>
/// Base hub with branch isolation and group management.
/// SOP §20: All dashboards require live synchronization.
/// Q2 Decision: Auto-negotiation (WebSocket primary, SSE + Long Polling fallback).
/// </summary>
[Authorize]
public abstract class BranchAwareHub : Hub
{
    protected ILogger Logger { get; }

    protected BranchAwareHub(ILogger logger) => Logger = logger;

    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        var branchId = Context.User?.FindFirstValue("branchId");
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = Context.User?.FindFirstValue(ClaimTypes.Name);

        // SOP §6.4: Operators and Admins join their branch group
        if ((role == Roles.Operator || role == Roles.Admin) && !string.IsNullOrEmpty(branchId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}");

        // Super Admin and Admin join all-branches group
        if (role == Roles.SuperAdmin || role == Roles.Admin)
            await Groups.AddToGroupAsync(Context.ConnectionId, "admin:all");

        // User-specific group for targeted notifications
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        Logger.LogInformation("Hub connected: {User} ({Role}) [{Hub}] - ConnectionId: {ConnectionId} - Branch: {BranchId}", 
            userName, role, GetType().Name, Context.ConnectionId, branchId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userName = Context.User?.FindFirstValue(ClaimTypes.Name);
        if (exception != null)
        {
            Logger.LogWarning(exception, "Hub disconnected with error: {User} [{Hub}] - ConnectionId: {ConnectionId}", 
                userName, GetType().Name, Context.ConnectionId);
        }
        else
        {
            Logger.LogInformation("Hub disconnected gracefully: {User} [{Hub}] - ConnectionId: {ConnectionId}", 
                userName, GetType().Name, Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>SOP §7: Session state sync — /hubs/sessions</summary>
public class SessionHub : BranchAwareHub
{
    public SessionHub(ILogger<SessionHub> logger) : base(logger) { }
}

/// <summary>SOP §9: Billing counter sync — /hubs/billing</summary>
public class BillingHub : BranchAwareHub
{
    public BillingHub(ILogger<BillingHub> logger) : base(logger) { }
}

/// <summary>SOP §8: Reservation state sync — /hubs/reservations</summary>
public class ReservationHub : BranchAwareHub
{
    public ReservationHub(ILogger<ReservationHub> logger) : base(logger) { }
}

/// <summary>SOP §17: PC state sync — /hubs/pc-status — Enhanced for Client Agent dual-connection</summary>
public class PcStatusHub : BranchAwareHub
{
    public PcStatusHub(ILogger<PcStatusHub> logger) : base(logger) { }

    /// <summary>Called by the Gaming PC Agent when it connects</summary>
    public async Task AgentConnected(string pcId, string connectionMode)
    {
        // Add agent to its own group so we can send targeted commands
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{pcId}");
        
        // Notify all Operator/Admin dashboards in this branch
        var branchId = Context.User?.FindFirstValue("branchId");
        if (!string.IsNullOrEmpty(branchId))
        {
            await Clients.Group($"branch:{branchId}").SendAsync("AgentStatusChanged", new
            {
                PcId = pcId,
                IsOnline = true,
                ConnectionMode = connectionMode,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        // Also notify SuperAdmin
        await Clients.Group("admin:all").SendAsync("AgentStatusChanged", new
        {
            PcId = pcId,
            IsOnline = true,
            ConnectionMode = connectionMode,
            Timestamp = DateTimeOffset.UtcNow
        });

        Logger.LogInformation("Agent connected: PC {PcId} in {Mode} mode", pcId, connectionMode);
    }

    /// <summary>Called by the Gaming PC Agent when it switches between LAN and Cloud</summary>
    public async Task AgentModeChanged(string pcId, string newMode)
    {
        var branchId = Context.User?.FindFirstValue("branchId");
        var payload = new
        {
            PcId = pcId,
            ConnectionMode = newMode,
            Timestamp = DateTimeOffset.UtcNow
        };

        if (!string.IsNullOrEmpty(branchId))
            await Clients.Group($"branch:{branchId}").SendAsync("AgentModeChanged", payload);

        await Clients.Group("admin:all").SendAsync("AgentModeChanged", payload);

        Logger.LogWarning("Agent mode changed: PC {PcId} -> {Mode}", pcId, newMode);
    }

    /// <summary>Called by Operator or Admin to unlock a Gaming PC</summary>
    public async Task SendUnlockCommand(string pcId, int durationMinutes, string? customerName)
    {
        await Clients.Group($"agent:{pcId}").SendAsync("UnlockSession", new
        {
            DurationMinutes = durationMinutes,
            CustomerName = customerName,
            Timestamp = DateTimeOffset.UtcNow
        });

        Logger.LogInformation("Unlock command sent to PC {PcId} for {Duration}min", pcId, durationMinutes);
    }

    /// <summary>Called by Operator or Admin to lock a Gaming PC</summary>
    public async Task SendLockCommand(string pcId)
    {
        await Clients.Group($"agent:{pcId}").SendAsync("LockSession", new
        {
            Timestamp = DateTimeOffset.UtcNow
        });

        Logger.LogInformation("Lock command sent to PC {PcId}", pcId);
    }

    /// <summary>Called by Admin to force shutdown a Gaming PC</summary>
    public async Task SendShutdownCommand(string pcId)
    {
        await Clients.Group($"agent:{pcId}").SendAsync("ForceShutdown", new
        {
            Timestamp = DateTimeOffset.UtcNow
        });

        Logger.LogWarning("Shutdown command sent to PC {PcId}", pcId);
    }

    /// <summary>Heartbeat from agent to keep connection alive</summary>
    public async Task AgentHeartbeat(string pcId, string mode)
    {
        Logger.LogDebug("Heartbeat from PC {PcId} in {Mode} mode", pcId, mode);
        await Task.CompletedTask;
    }
}

/// <summary>SOP §12: Food order sync — /hubs/food-orders</summary>
public class FoodOrderHub : BranchAwareHub
{
    public FoodOrderHub(ILogger<FoodOrderHub> logger) : base(logger) { }
}

/// <summary>SOP §10/§11: Cash register/desk sync — /hubs/cash</summary>
public class CashHub : BranchAwareHub
{
    public CashHub(ILogger<CashHub> logger) : base(logger) { }
}

/// <summary>Cross-cutting alerts and notifications — /hubs/notifications</summary>
public class NotificationHub : BranchAwareHub
{
    public NotificationHub(ILogger<NotificationHub> logger) : base(logger) { }
}
