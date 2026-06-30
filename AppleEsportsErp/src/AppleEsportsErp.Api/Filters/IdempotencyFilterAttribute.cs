using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using AppleEsportsErp.Application.DTOs.Common;

namespace AppleEsportsErp.Api.Filters;

/// <summary>
/// Hardening C.1: Prevents duplicate submissions (e.g. double payments from double-clicks)
/// Caches the successful response of an operation using the provided Idempotency-Key.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    private const string IdempotencyKeyHeader = "X-Idempotency-Key";
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(24);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        // Ensure key is provided
        if (!request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues) || string.IsNullOrEmpty(keyValues.ToString()))
        {
            context.Result = new BadRequestObjectResult(ApiResponse.Fail("Missing X-Idempotency-Key header.", "IDEMPOTENCY_KEY_MISSING"));
            return;
        }

        var idempotencyKey = keyValues.ToString();
        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();

        // Check if we already processed this exact request
        var cacheKey = $"idempotent_{request.Path}_{idempotencyKey}";
        if (cache.TryGetValue(cacheKey, out ObjectResult? cachedResult))
        {
            // Return cached response instead of re-executing
            context.HttpContext.Response.Headers.Append("X-Idempotency-Cached", "true");
            context.Result = cachedResult;
            return;
        }

        // Execute action
        var executedContext = await next();

        // If successful, cache the result
        if (executedContext.Exception == null && executedContext.Result is ObjectResult objectResult)
        {
            if (objectResult.StatusCode >= 200 && objectResult.StatusCode < 300)
            {
                cache.Set(cacheKey, objectResult, _cacheDuration);
            }
        }
    }
}
