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

    /// <summary>
    /// What to ask before opening the drawer: a float from the first shift of the day, nothing
    /// at all from every shift after it.
    ///
    /// Not shift-scoped, deliberately. The drawer belongs to the branch and the trading day, not
    /// to whoever is standing at it, and an operator whose shift has just been issued by a
    /// takeover needs this answer straight away.
    /// </summary>
    [HttpGet("opening")]
    public async Task<IActionResult> GetOpening()
    {
        var result = await _cashRegisterService.GetOpeningAsync(GetBranchId());
        return Ok(ApiResponse<RegisterOpeningDto>.Ok(result));
    }

    [HttpPost("open")]
    public async Task<IActionResult> OpenRegister([FromBody] OpenRegisterDto dto)
    {
        var result = await _cashRegisterService.OpenRegisterAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), dto);
        return Ok(ApiResponse<OpenRegisterResultDto>.Ok(result));
    }

    [HttpPost("transaction")]
    public async Task<IActionResult> AddTransaction([FromBody] AddCashTransactionDto dto)
    {
        var result = await _cashRegisterService.AddTransactionAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), dto);
        return Ok(ApiResponse<CashRegisterDto>.Ok(result));
    }
}


