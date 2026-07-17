using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Billing;

namespace AppleEsportsErp.Application.Interfaces;

public interface IBillingService
{
    Task<PaginatedResult<BillDto>> GetActiveBillsAsync(Guid branchId, int page = 1, int pageSize = 50);
    Task<List<BillDto>> GetDeferredBillsAsync(Guid branchId);
    Task<BillDto> GetBillAsync(Guid branchId, Guid id);
    Task<BillDto> GetBillByNumberAsync(Guid branchId, string billNumber);
    Task<BillDto> ApplyDiscountAsync(Guid branchId, Guid superAdminId, Guid id, ApplyDiscountDto dto);
    Task<BillDto> ProcessPaymentAsync(Guid branchId, Guid operatorId, Guid shiftId, Guid id, ProcessPaymentDto dto);
    Task<BillDto> RemoveBillItemAsync(Guid branchId, Guid operatorId, Guid billId, Guid billItemId);
}
