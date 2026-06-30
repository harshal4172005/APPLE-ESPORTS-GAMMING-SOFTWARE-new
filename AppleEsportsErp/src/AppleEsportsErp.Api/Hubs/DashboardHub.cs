using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Hubs;

[Authorize]
public class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        
        if (role == "SuperAdmin")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "SuperAdmin");
        }
        else if (role == "Operator")
        {
            var branchId = Context.User?.FindFirst("branchId")?.Value;
            if (!string.IsNullOrEmpty(branchId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}");
            }
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        
        if (role == "SuperAdmin")
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "SuperAdmin");
        }
        else if (role == "Operator")
        {
            var branchId = Context.User?.FindFirst("branchId")?.Value;
            if (!string.IsNullOrEmpty(branchId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"branch:{branchId}");
            }
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
