using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
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
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<AppleEsportsErp.Api.Hubs.PcOverlayHub> _pcOverlayHub;
    private readonly AppleEsportsErp.Application.Interfaces.IBillingService _billingService;

    public SessionsController(
        ISessionService sessionService,
        Microsoft.AspNetCore.SignalR.IHubContext<AppleEsportsErp.Api.Hubs.PcOverlayHub> pcOverlayHub,
        AppleEsportsErp.Application.Interfaces.IBillingService billingService)
    {
        _sessionService = sessionService;
        _pcOverlayHub = pcOverlayHub;
        _billingService = billingService;
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
        PcOverlayHub.PendingWalkinRequests.TryRemove(result.PcName, out _);
        return Ok(ApiResponse<SessionDto>.Ok(result));
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> StopSession(Guid id, [FromBody] StopSessionDto? dto = null)
    {
        Console.WriteLine($"[SessionsController] StopSession called for {id}. dto is null? {dto == null}. DeferPayment: {dto?.DeferPayment}");
        var result = await _sessionService.StopSessionAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, dto?.DeferPayment ?? false);
        AppleEsportsErp.Api.Hubs.PcOverlayHub.PendingWalkinRequests.TryRemove(result.PcId.ToString(), out _);
        AppleEsportsErp.Api.Hubs.PcOverlayHub.PendingWalkinRequests.TryRemove(result.PcName, out _);

        // Auto-trigger wallet approval request for members
        if (result.MemberId != null)
        {
            var bill = await _billingService.GetBillAsync(GetBranchId(), result.BillId);
            if (bill != null && bill.Status != AppleEsportsErp.Domain.Enums.BillStatus.Completed && bill.TotalAmount > 0)
            {
                var approvalToken = Guid.NewGuid();
                var pendingRequest = new AppleEsportsErp.Application.DTOs.Billing.PendingWalletApproval
                {
                    BillId = result.BillId,
                    OperatorId = await this.GetOperatorIdAsync(),
                    ShiftId = await this.GetShiftIdAsync(),
                    BranchId = GetBranchId(),
                    Amount = bill.TotalAmount
                };

                BillingController.PendingApprovals.TryAdd(approvalToken, pendingRequest);

                await _pcOverlayHub.Clients.Group($"pc:{result.PcId}").SendAsync("ReceiveWalletApprovalRequest", new
                {
                    billId = result.BillId,
                    amount = bill.TotalAmount,
                    approvalToken = approvalToken
                });
            }
        }

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


