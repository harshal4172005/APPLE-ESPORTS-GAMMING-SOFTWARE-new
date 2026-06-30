using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Sessions;

namespace AppleEsportsErp.Application.Interfaces;

public interface ISessionService
{
    Task<PaginatedResult<SessionDto>> GetActiveSessionsAsync(Guid branchId, int page, int pageSize);
    Task<SessionDto> StartSessionAsync(Guid branchId, Guid operatorId, Guid shiftId, SessionStartDto dto);
    Task<SessionDto> StopSessionAsync(Guid branchId, Guid operatorId, Guid sessionId, bool deferPayment = false);
    Task<SessionDto> ExtendSessionAsync(Guid branchId, Guid operatorId, Guid sessionId, SessionExtendDto dto);
    Task<SessionDto> TransferSessionAsync(Guid branchId, Guid operatorId, Guid sessionId, SessionTransferDto dto);
}
