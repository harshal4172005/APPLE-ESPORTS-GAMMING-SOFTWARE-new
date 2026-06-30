using System;
using System.Threading.Tasks;
using AppleEsportsErp.Application.DTOs.SystemDesks;

namespace AppleEsportsErp.Application.Interfaces;

public interface ISystemDesksService
{
    Task<OnlineDeskSummaryDto> GetActiveOnlineDeskAsync(Guid branchId, Guid shiftId);
    Task<WalletDeskSummaryDto> GetActiveWalletDeskAsync(Guid branchId, Guid shiftId);
}
