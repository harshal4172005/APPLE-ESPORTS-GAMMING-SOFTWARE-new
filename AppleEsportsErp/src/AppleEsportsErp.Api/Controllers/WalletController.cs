using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Wallets;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/wallets")]
[Authorize]
[BranchIsolation]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet("{memberId:guid}")]
    public async Task<IActionResult> GetWalletHistory(Guid memberId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _walletService.GetWalletHistoryAsync(memberId, page, pageSize);
        return Ok(ApiResponse<PaginatedResult<WalletTransactionDto>>.Ok(result));
    }

    [HttpPost("{memberId:guid}/topup")]
    [Idempotent]
    public async Task<IActionResult> TopUpWallet(Guid memberId, [FromBody] TopUpWalletDto dto)
    {
        var result = await _walletService.TopUpWalletAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), memberId, dto);
        return Ok(ApiResponse<WalletTransactionDto>.Ok(result));
    }

    [HttpPost("{memberId:guid}/deduct")]
    [Idempotent]
    public async Task<IActionResult> DeductWallet(Guid memberId, [FromBody] DeductWalletDto dto)
    {
        var result = await _walletService.DeductWalletAsync(GetBranchId(), (await this.GetOperatorIdAsync()), memberId, dto);
        return Ok(ApiResponse<WalletTransactionDto>.Ok(result));
    }
}


