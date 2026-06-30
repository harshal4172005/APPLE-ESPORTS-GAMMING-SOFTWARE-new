using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Infrastructure.Services;

/// <summary>
/// SOP §22: Immutable Audit Trail — INSERT only, failures never block operations.
/// Maps from audit.js logAudit function.
/// </summary>
public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(AuditEntry entry)
    {
        try
        {
            var auditLog = new AuditLog
            {
                UserId = entry.UserId,
                OperatorId = entry.OperatorId,
                UserRole = entry.UserRole,
                UserName = entry.UserName,
                Action = entry.Action,
                TargetType = entry.TargetType,
                TargetId = entry.TargetId,
                BranchId = entry.BranchId,
                BranchName = entry.BranchName,
                Details = entry.Details != null ? JsonSerializer.Serialize(entry.Details) : null,
                IpAddress = entry.IpAddress,
                DeviceInfo = entry.DeviceInfo != null ? JsonSerializer.Serialize(entry.DeviceInfo) : null,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync();

            _logger.LogInformation("AUDIT: {Action} by {User} ({Role})", entry.Action, entry.UserName, entry.UserRole);
        }
        catch (Exception ex)
        {
            // SOP: Audit logging failures should never block operations but MUST be logged
            _logger.LogError(ex, "AUDIT LOG FAILURE — CRITICAL: {Action} by {User}", entry.Action, entry.UserName);
        }
    }
}
