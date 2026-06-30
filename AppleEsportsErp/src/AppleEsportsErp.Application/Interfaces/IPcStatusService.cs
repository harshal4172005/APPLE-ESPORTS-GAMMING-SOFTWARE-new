using AppleEsportsErp.Application.DTOs.PcStatus;

namespace AppleEsportsErp.Application.Interfaces;

public interface IPcStatusService
{
    Task<IEnumerable<PcStatusDto>> GetBranchPcStatusesAsync(Guid branchId);
    Task<PcStatusDto> GetPcStatusAsync(Guid pcId);
    Task BroadcastPcStatusChangeAsync(Guid branchId, Guid pcId);
}
