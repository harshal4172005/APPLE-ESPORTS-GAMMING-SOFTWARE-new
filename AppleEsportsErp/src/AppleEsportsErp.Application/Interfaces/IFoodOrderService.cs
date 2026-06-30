using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.FoodOrders;

namespace AppleEsportsErp.Application.Interfaces;

public interface IFoodOrderService
{
    Task<PaginatedResult<FoodOrderDto>> GetActiveOrdersAsync(Guid branchId, int page = 1, int pageSize = 50);
    Task<FoodOrderDto> GetOrderAsync(Guid branchId, Guid id);
    Task<FoodOrderDto> PlaceOrderAsync(Guid branchId, Guid operatorId, Guid shiftId, CreateFoodOrderDto dto);
    Task<FoodOrderDto> UpdateOrderStatusAsync(Guid branchId, Guid operatorId, Guid id, UpdateOrderStatusDto dto);
}
