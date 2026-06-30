using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Enums;
using System.Security.Claims;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.Constants;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/system-desks")]
[Authorize]
[BranchIsolation]
public class SystemDesksController : ControllerBase
{
    private readonly ISystemDesksService _systemDesksService;

    public SystemDesksController(ISystemDesksService systemDesksService)
    {
        _systemDesksService = systemDesksService;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet("online/active")]
    [Authorize(Roles = Roles.Operator + "," + Roles.SuperAdmin)]
    public async Task<IActionResult> GetActiveOnlineDesk()
    {
        try
        {
            var result = await _systemDesksService.GetActiveOnlineDeskAsync(GetBranchId(), await this.GetShiftIdAsync());
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("wallet/active")]
    [Authorize(Roles = Roles.Operator + "," + Roles.SuperAdmin)]
    public async Task<IActionResult> GetActiveWalletDesk()
    {
        try
        {
            var result = await _systemDesksService.GetActiveWalletDeskAsync(GetBranchId(), await this.GetShiftIdAsync());
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
