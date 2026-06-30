using System.Security.Claims;
using AppleEsportsErp.Application.Constants;
using Serilog.Context;

namespace AppleEsportsErp.Api.Middleware;

/// <summary>
/// Hardening C.1: Generates a unique RequestId and CorrelationId.
/// Injects OperatorId, BranchId, and ShiftId into the logging context for trace analysis.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        var requestId = Guid.NewGuid().ToString("N");

        context.TraceIdentifier = requestId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Extract operator identity for logs
        var user = context.User;
        var operatorId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var branchId = user?.FindFirst("branchId")?.Value ?? "Global";
        var shiftId = user?.FindFirst("shiftId")?.Value ?? "None";
        var role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "None";

        context.Items["CorrelationId"] = correlationId;
        context.Items["RequestId"] = requestId;

        // Push properties to Serilog LogContext
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("OperatorId", operatorId))
        using (LogContext.PushProperty("BranchId", branchId))
        using (LogContext.PushProperty("ShiftId", shiftId))
        using (LogContext.PushProperty("Role", role))
        {
            await _next(context);
        }
    }
}
