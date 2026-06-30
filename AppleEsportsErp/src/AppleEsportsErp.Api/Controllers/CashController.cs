using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Cash;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/cash")]
[Authorize]
[BranchIsolation]
public class CashController : ControllerBase
{
    private readonly ICashRegisterService _cashRegisterService;

    public CashController(ICashRegisterService cashRegisterService)
    {
        _cashRegisterService = cashRegisterService;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveRegister()
    {
        var result = await _cashRegisterService.GetActiveRegisterAsync(GetBranchId(), (await this.GetShiftIdAsync()));
        return Ok(ApiResponse<CashRegisterDto>.Ok(result));
    }

    [HttpPost("open")]
    public async Task<IActionResult> OpenRegister([FromBody] OpenRegisterDto dto)
    {
        var result = await _cashRegisterService.OpenRegisterAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), dto);
        return Ok(ApiResponse<CashRegisterDto>.Ok(result));
    }

    [HttpPost("transaction")]
    public async Task<IActionResult> AddTransaction([FromBody] AddCashTransactionDto dto)
    {
        var result = await _cashRegisterService.AddTransactionAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), dto);
        return Ok(ApiResponse<CashRegisterDto>.Ok(result));
    }
}


