using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Api.Hubs;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Sessions;
using AppleEsportsErp.Application.Interfaces;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
[BranchIsolation]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }
    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet]
    public async Task<IActionResult> GetActiveSessions([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _sessionService.GetActiveSessionsAsync(GetBranchId(), page, pageSize);
        return Ok(ApiResponse<PaginatedResult<SessionDto>>.Ok(result));
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] SessionStartDto dto)
    {
        var result = await _sessionService.StartSessionAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), dto);
        // Remove any pending walk-in request for this PC now that a session has started
        PcOverlayHub.PendingWalkinRequests.TryRemove(dto.PcId.ToString(), out _);
        return Ok(ApiResponse<SessionDto>.Ok(result));
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> StopSession(Guid id, [FromBody] StopSessionDto? dto = null)
    {
        Console.WriteLine($"[SessionsController] StopSession called for {id}. dto is null? {dto == null}. DeferPayment: {dto?.DeferPayment}");
        var result = await _sessionService.StopSessionAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, dto?.DeferPayment ?? false);
        PcOverlayHub.PendingWalkinRequests.TryRemove(result.PcId.ToString(), out _);
        return Ok(ApiResponse<SessionDto>.Ok(result));
    }

    [HttpPost("{id}/extend")]
    public async Task<IActionResult> ExtendSession(Guid id, [FromBody] SessionExtendDto dto)
    {
        var result = await _sessionService.ExtendSessionAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, dto);
        return Ok(ApiResponse<SessionDto>.Ok(result));
    }

    [HttpPost("{id}/transfer")]
    public async Task<IActionResult> TransferSession(Guid id, [FromBody] SessionTransferDto dto)
    {
        var result = await _sessionService.TransferSessionAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, dto);
        return Ok(ApiResponse<SessionDto>.Ok(result));
    }
}


