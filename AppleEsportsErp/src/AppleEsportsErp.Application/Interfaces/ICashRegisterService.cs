using AppleEsportsErp.Application.DTOs.Cash;

namespace AppleEsportsErp.Application.Interfaces;

public interface ICashRegisterService
{
    Task<CashRegisterDto> GetActiveRegisterAsync(Guid branchId, Guid shiftId);
    Task<CashRegisterDto> OpenRegisterAsync(Guid branchId, Guid operatorId, Guid shiftId, OpenRegisterDto dto);
    Task<CashRegisterDto> AddTransactionAsync(Guid branchId, Guid operatorId, Guid shiftId, AddCashTransactionDto dto);
}
