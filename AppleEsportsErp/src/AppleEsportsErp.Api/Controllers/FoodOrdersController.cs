using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.FoodOrders;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/food-orders")]
[Authorize]
[BranchIsolation]
public class FoodOrdersController : ControllerBase
{
    private readonly IFoodOrderService _foodOrderService;

    public FoodOrdersController(IFoodOrderService foodOrderService)
    {
        _foodOrderService = foodOrderService;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet]
    public async Task<IActionResult> GetActiveOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _foodOrderService.GetActiveOrdersAsync(GetBranchId(), page, pageSize);
        return Ok(ApiResponse<PaginatedResult<FoodOrderDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var result = await _foodOrderService.GetOrderAsync(GetBranchId(), id);
        return Ok(ApiResponse<FoodOrderDto>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] CreateFoodOrderDto dto)
    {
        var result = await _foodOrderService.PlaceOrderAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), dto);
        return Ok(ApiResponse<FoodOrderDto>.Ok(result));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusDto dto)
    {
        var result = await _foodOrderService.UpdateOrderStatusAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, dto);
        return Ok(ApiResponse<FoodOrderDto>.Ok(result));
    }
}


