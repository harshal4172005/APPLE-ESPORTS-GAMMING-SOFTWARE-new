using AppleEsportsErp.Application.DTOs.PcManagement;

namespace AppleEsportsErp.Application.Interfaces;

public interface IPcManagementService
{
    Task<List<PcDto>> GetPcsByBranchAsync(Guid branchId, bool includeDeleted = false);
    Task<PcDto> AddPcAsync(Guid branchId, Guid superAdminId, CreatePcDto dto);
    Task<PcDto> UpdatePcAsync(Guid pcId, Guid superAdminId, UpdatePcDto dto);
    Task<PcDto> TransferPcAsync(Guid pcId, Guid newBranchId, Guid superAdminId);
    Task<PcDto> MarkMaintenanceAsync(Guid pcId, Guid superAdminId, bool isMaintenance);
    Task DeletePcAsync(Guid pcId, Guid superAdminId);
}
