using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Application.Exceptions;

namespace AppleEsportsErp.Api.Filters;

/// <summary>
/// SOP §6.4: Operators can ONLY access THEIR ASSIGNED BRANCH.
/// Backend ALWAYS filters WHERE branch_id = operator_branch.
/// Maps from branchIsolation.js enforceBranchIsolation middleware.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class BranchIsolationAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) return;

        var role = user.FindFirstValue(ClaimTypes.Role);

        // Super Admin can access any branch via query param or header
        if (role == Roles.SuperAdmin)
        {
            var branchId = context.HttpContext.Request.Query["branch_id"].FirstOrDefault()
                        ?? context.HttpContext.Request.Query["branchId"].FirstOrDefault()
                        ?? context.HttpContext.Request.Headers["X-Branch-Id"].FirstOrDefault();
            context.HttpContext.Items["BranchId"] = branchId;
            return;
        }

        // Members are not branch-scoped — skip isolation
        if (role == "Member") return;

        // Operator — enforce their assigned branch
        var assignedBranch = user.FindFirstValue("branchId");
        if (string.IsNullOrEmpty(assignedBranch))
        {
            throw new BranchIsolationException("No branch assigned to this operator.");
        }

        // Check if operator tries to access another branch via params/query/body
        var requestedBranch = context.HttpContext.Request.Query["branch_id"].FirstOrDefault()
                           ?? context.HttpContext.Request.Query["branchId"].FirstOrDefault()
                           ?? context.HttpContext.Request.RouteValues["branchId"]?.ToString();

        if (!string.IsNullOrEmpty(requestedBranch) && requestedBranch != assignedBranch)
        {
            throw new BranchIsolationException("Access denied. You can only access your assigned branch.");
        }

        // Lock branch to operator's assigned branch
        context.HttpContext.Items["BranchId"] = assignedBranch;
    }
}
