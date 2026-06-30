using AppleEsportsErp.Application.DTOs.Cash;

namespace AppleEsportsErp.Application.Interfaces;

public interface ICashDeskService
{
    Task StartVerificationAsync(Guid branchId, Guid operatorId, Guid shiftId);
    Task<DenominationCountDto> SubmitDenominationsAsync(Guid branchId, Guid operatorId, Guid shiftId, SubmitDenominationDto dto);
    Task CloseRegisterAsync(Guid branchId, Guid operatorId, Guid shiftId, Guid cashRegisterId);
}
