using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppleEsportsErp.Application.DTOs.Sync;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Infrastructure.Services;

/// <summary>
/// Decentralized LAN Offline Architecture sync service.
/// Persists offline telemetry and attempts to reconcile it against live DB records.
/// </summary>
public class OfflineSyncService : IOfflineSyncService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<OfflineSyncService> _logger;

    public OfflineSyncService(AppDbContext db, IAuditService audit, ILogger<OfflineSyncService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<SyncResultDto> SyncOfflineSessionAsync(SyncOfflineSessionDto dto, Guid? operatorId)
    {
        // Attempt to resolve the PcId to a live Pc record for reconciliation
        Guid? resolvedPcId = null;
        Guid? branchId = null;

        if (Guid.TryParse(dto.PcId, out var pcGuid))
        {
            var pc = await _db.Pcs.FirstOrDefaultAsync(p => p.Id == pcGuid && !p.IsDeleted);
            if (pc != null) { resolvedPcId = pc.Id; branchId = pc.BranchId; }
        }

        if (resolvedPcId == null)
        {
            // Fallback: match by PcNumber string
            var pc = await _db.Pcs.FirstOrDefaultAsync(p => p.PcNumber == dto.PcId && !p.IsDeleted);
            if (pc != null) { resolvedPcId = pc.Id; branchId = pc.BranchId; }
        }

        var syncStatus = resolvedPcId.HasValue ? "reconciled" : "pending";

        var record = new OfflineSyncSession
        {
            Id = Guid.NewGuid(),
            PcId = dto.PcId,
            ResolvedPcId = resolvedPcId,
            BranchId = branchId,
            DurationSeconds = dto.DurationSeconds,
            OfflineStartTime = dto.OfflineStartTime,
            SessionType = dto.SessionType,
            SyncStatus = syncStatus,
            Notes = dto.Notes,
            RawPayload = JsonSerializer.Serialize(dto),
            SyncedAt = DateTimeOffset.UtcNow,
            SubmittedByOperatorId = operatorId,
        };

        _db.OfflineSyncSessions.Add(record);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[OfflineSync] Session received: PC={PcId}, Duration={Dur}s, Status={Status}",
            dto.PcId, dto.DurationSeconds, syncStatus);

        await TryAuditAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = operatorId.HasValue ? "operator" : "system",
            UserName = operatorId.HasValue ? "Operator" : $"PC:{dto.PcId}"[..Math.Min($"PC:{dto.PcId}".Length, 100)],
            Action = "offline_session_sync",
            BranchId = branchId,
            Details = new { pcId = dto.PcId, durationSeconds = dto.DurationSeconds, syncId = record.Id, status = syncStatus },
        });

        return new SyncResultDto
        {
            Success = true,
            SyncId = record.Id,
            Status = syncStatus,
            Message = resolvedPcId.HasValue
                ? "Offline session reconciled successfully."
                : "Offline session stored and queued for manual reconciliation.",
        };
    }

    public async Task<SyncResultDto> SyncOfflineBillingAsync(SyncOfflineBillingDto dto, Guid operatorId)
    {
        // Attempt to resolve the BranchId string to a live Branch record
        Guid? resolvedBranchId = null;

        if (!string.IsNullOrEmpty(dto.BranchId) && Guid.TryParse(dto.BranchId, out var branchGuid))
        {
            var exists = await _db.Branches.AnyAsync(b => b.Id == branchGuid);
            if (exists) resolvedBranchId = branchGuid;
        }

        var syncStatus = resolvedBranchId.HasValue ? "reconciled" : "pending";

        var record = new OfflineSyncBilling
        {
            Id = Guid.NewGuid(),
            BranchId = dto.BranchId,
            ResolvedBranchId = resolvedBranchId,
            Amount = dto.Amount,
            TransactionType = dto.TransactionType,
            Timestamp = dto.Timestamp,
            SyncStatus = syncStatus,
            Notes = dto.Notes,
            RawPayload = JsonSerializer.Serialize(dto),
            SyncedAt = DateTimeOffset.UtcNow,
            SubmittedByOperatorId = operatorId,
        };

        _db.OfflineSyncBillings.Add(record);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[OfflineSync] Billing received: Branch={BranchId}, Amount={Amount}, Status={Status}",
            dto.BranchId, dto.Amount, syncStatus);

        await TryAuditAsync(new AuditEntry
        {
            OperatorId = operatorId,
            UserRole = "operator",
            UserName = "Operator",
            Action = "offline_billing_sync",
            BranchId = resolvedBranchId,
            Details = new { branchId = dto.BranchId, amount = dto.Amount, syncId = record.Id, status = syncStatus },
        });

        return new SyncResultDto
        {
            Success = true,
            SyncId = record.Id,
            Status = syncStatus,
            Message = resolvedBranchId.HasValue
                ? "Offline billing reconciled successfully."
                : "Offline billing stored and queued for manual reconciliation.",
        };
    }

    private async Task TryAuditAsync(AuditEntry entry)
    {
        try { await _audit.LogAsync(entry); }
        catch (Exception ex) { _logger.LogWarning(ex, "[OfflineSync] Audit log failed (non-fatal)"); }
    }
}
