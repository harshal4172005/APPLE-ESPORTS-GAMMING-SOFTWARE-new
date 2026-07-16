using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using AppleEsportsErp.Application.Constants;

namespace AppleEsportsErp.Api.Filters;

/// <summary>
/// SOP §19.2: Dashboard Permission Control per operator.
/// Super Admin always has full access. Operators checked against their dashboard_permissions JWT claim.
/// Maps from roles.js requireDashboardAccess middleware.
/// </summary>
public class DashboardRequirement : IAuthorizationRequirement
{
    public string DashboardKey { get; }
    public DashboardRequirement(string dashboardKey) => DashboardKey = dashboardKey;
}

public class DashboardAuthorizationHandler : AuthorizationHandler<DashboardRequirement>
{
    private readonly ILogger<DashboardAuthorizationHandler> _logger;

    public DashboardAuthorizationHandler(ILogger<DashboardAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DashboardRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);

        // Super Admin and Admin always have full access
        if (role == Roles.SuperAdmin || role == Roles.Admin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if dashboard is admin-only (block Operators)
        if (role == Roles.Operator && Dashboards.AdminOnly.Contains(requirement.DashboardKey))
        {
            _logger.LogWarning("Operator attempted admin-only dashboard: {Dashboard}", requirement.DashboardKey);
            context.Fail();
            return Task.CompletedTask;
        }

        // Check operator's dashboard permissions from JWT claim
        var permissionsClaim = context.User.FindFirstValue("dashboardPermissions");
        if (string.IsNullOrEmpty(permissionsClaim))
        {
            context.Fail();
            return Task.CompletedTask;
        }

        try
        {
            var permissions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(permissionsClaim);
            if (permissions != null && permissions.TryGetValue(requirement.DashboardKey, out var hasAccess) && hasAccess)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("Dashboard permission denied: {Dashboard}", requirement.DashboardKey);
                context.Fail();
            }
        }
        catch
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}
