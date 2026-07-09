using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Credits;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Api.Extensions;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[BranchIsolation]
public class CreditsController : ControllerBase
{
    private readonly ICreditService _creditService;

    public CreditsController(ICreditService creditService)
    {
        _creditService = creditService;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet]
    public async Task<IActionResult> GetCredits([FromQuery] string status = "pending", [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await _creditService.GetCreditsAsync(GetBranchId(), status, page, pageSize);
            return Ok(ApiResponse<PaginatedResult<CreditDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(@"c:\Users\harsh\Desktop\credit_error.txt", ex.ToString());
            throw;
        }
    }

    [HttpPost("{id}/clear")]
    public async Task<IActionResult> ClearCredit(Guid id, [FromBody] ClearCreditDto dto)
    {
        var result = await _creditService.ClearCreditAsync(
            GetBranchId(),
            await this.GetOperatorIdAsync(),
            await this.GetShiftIdAsync(),
            id,
            dto
        );
        return Ok(ApiResponse<CreditDto>.Ok(result));
    }
}
