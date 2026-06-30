using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.Exceptions;

namespace AppleEsportsErp.Api.Middleware;

/// <summary>
/// Global exception handler — maps from errorHandler.js.
/// Catches all unhandled exceptions and returns structured JSON responses.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning("Application error: {Code} — {Message}", ex.Code, ex.Message);
            await WriteErrorResponse(context, (int)ex.StatusCode, ex.Message, ex.Code);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errors = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }).ToArray();
            var correlationId = context.Items["CorrelationId"]?.ToString();
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = "Validation failed",
                code = "VALIDATION_ERROR",
                details = errors,
                correlationId,
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict occurred");
            await WriteErrorResponse(context, 409, "The record was modified by another operator. Please refresh and try again.", "CONCURRENCY_CONFLICT");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await WriteErrorResponse(context, 500, "Internal server error", "INTERNAL_ERROR");
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string error, string code)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            error,
            code,
            correlationId,
        });
    }
}
