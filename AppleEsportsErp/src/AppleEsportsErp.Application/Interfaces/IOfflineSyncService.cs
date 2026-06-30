using AppleEsportsErp.Application.DTOs.Sync;

namespace AppleEsportsErp.Application.Interfaces;

/// <summary>
/// Decentralized LAN Offline Architecture sync service.
/// Accepts telemetry and billing data from offline PCs and reconciles it against live data.
/// </summary>
public interface IOfflineSyncService
{
    /// <summary>Accept offline session telemetry from a Gaming PC.</summary>
    Task<SyncResultDto> SyncOfflineSessionAsync(SyncOfflineSessionDto dto, Guid? operatorId);

    /// <summary>Accept offline cash transaction from an Operator's PC.</summary>
    Task<SyncResultDto> SyncOfflineBillingAsync(SyncOfflineBillingDto dto, Guid operatorId);
}
