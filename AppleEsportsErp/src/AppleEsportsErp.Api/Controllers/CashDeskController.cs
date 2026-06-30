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
[Route("api/cash-desk")]
[Authorize]
[BranchIsolation]
public class CashDeskController : ControllerBase
{
    private readonly ICashDeskService _cashDeskService;

    public CashDeskController(ICashDeskService cashDeskService)
    {
        _cashDeskService = cashDeskService;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpPost("verify-start")]
    [Idempotent]
    public async Task<IActionResult> StartVerification()
    {
        await _cashDeskService.StartVerificationAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()));
        return Ok(new { success = true, message = "Verification started, register locked." });
    }

    [HttpPost("denominations")]
    [Idempotent]
    public async Task<IActionResult> SubmitDenominations([FromBody] SubmitDenominationDto dto)
    {
        var result = await _cashDeskService.SubmitDenominationsAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), dto);
        return Ok(ApiResponse<DenominationCountDto>.Ok(result));
    }

    [HttpPost("close/{registerId:guid}")]
    [Idempotent]
    public async Task<IActionResult> CloseRegister(Guid registerId)
    {
        await _cashDeskService.CloseRegisterAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), registerId);
        return Ok(new { success = true, message = "Register closed successfully" });
    }
}


