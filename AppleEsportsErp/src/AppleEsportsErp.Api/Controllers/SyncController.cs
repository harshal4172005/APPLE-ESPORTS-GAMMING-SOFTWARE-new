using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Sync;
using AppleEsportsErp.Application.Interfaces;

namespace AppleEsportsErp.Api.Controllers;

/// <summary>
/// Decentralized LAN Offline Architecture sync endpoints.
/// Accepts telemetry and billing data from offline Gaming PCs and Operator stations
/// and reconciles them against live data once connectivity is restored.
/// </summary>
[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly IOfflineSyncService _syncService;

    public SyncController(IOfflineSyncService syncService)
    {
        _syncService = syncService;
    }

    /// <summary>
    /// POST /api/sync/offline-session
    /// Accept offline session telemetry from an individual Gaming PC.
    /// AllowAnonymous because a PC's regular JWT may have expired during an extended outage;
    /// if a valid token is present the operator ID is captured for audit purposes.
    /// </summary>
    [HttpPost("offline-session")]
    [AllowAnonymous]
    public async Task<IActionResult> SyncOfflineSession([FromBody] SyncOfflineSessionDto dto)
    {
        Guid? operatorId = null;
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (idClaim != null && Guid.TryParse(idClaim, out var parsed))
            operatorId = parsed;

        var result = await _syncService.SyncOfflineSessionAsync(dto, operatorId);
        return Ok(ApiResponse<SyncResultDto>.Ok(result));
    }

    /// <summary>
    /// POST /api/sync/offline-billing
    /// Accept offline cash transactions from the Operator's PC.
    /// Requires an authenticated operator or admin (may use the emergency offline token).
    /// </summary>
    [HttpPost("offline-billing")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> SyncOfflineBilling([FromBody] SyncOfflineBillingDto dto)
    {
        var operatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _syncService.SyncOfflineBillingAsync(dto, operatorId);
        return Ok(ApiResponse<SyncResultDto>.Ok(result));
    }
}
