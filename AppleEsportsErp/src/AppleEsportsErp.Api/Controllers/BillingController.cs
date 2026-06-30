using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Billing;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/bills")]
[Authorize]
[BranchIsolation]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly IHubContext<AppleEsportsErp.Api.Hubs.PcOverlayHub> _pcOverlayHub;
    
    public static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, AppleEsportsErp.Application.DTOs.Billing.PendingWalletApproval> PendingApprovals = new();

    public BillingController(IBillingService billingService, IHubContext<AppleEsportsErp.Api.Hubs.PcOverlayHub> pcOverlayHub)
    {
        _billingService = billingService;
        _pcOverlayHub = pcOverlayHub;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet]
    public async Task<IActionResult> GetActiveBills([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _billingService.GetActiveBillsAsync(GetBranchId(), page, pageSize);
        return Ok(ApiResponse<PaginatedResult<BillDto>>.Ok(result));
    }

    [HttpGet("deferred")]
    public async Task<IActionResult> GetDeferredBills()
    {
        var result = await _billingService.GetDeferredBillsAsync(GetBranchId());
        return Ok(new { success = true, data = result });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBill(Guid id)
    {
        var result = await _billingService.GetBillAsync(GetBranchId(), id);
        return Ok(ApiResponse<BillDto>.Ok(result));
    }

    [HttpPost("{id:guid}/discount")]
    [Idempotent]
    [Authorize] // Replaced strict policy with inline check
    public async Task<IActionResult> ApplyDiscount(Guid id, [FromBody] ApplyDiscountDto dto)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var permissionsStr = User.FindFirstValue("dashboardPermissions");
        
        bool canDiscount = role == AppleEsportsErp.Application.Constants.Roles.SuperAdmin;
        if (!canDiscount && role == AppleEsportsErp.Application.Constants.Roles.Admin && !string.IsNullOrEmpty(permissionsStr))
        {
            try
            {
                var permissions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(permissionsStr);
                if (permissions != null && permissions.TryGetValue("discount", out var hasDiscount) && hasDiscount)
                {
                    canDiscount = true;
                }
            }
            catch { }
        }

        if (!canDiscount) return Forbid();

        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _billingService.ApplyDiscountAsync(GetBranchId(), adminId, id, dto);
        return Ok(ApiResponse<BillDto>.Ok(result));
    }

    [HttpPost("{id:guid}/pay")]
    [Idempotent]
    public async Task<IActionResult> ProcessPayment(Guid id, [FromBody] ProcessPaymentDto dto)
    {
        var result = await _billingService.ProcessPaymentAsync(GetBranchId(), (await this.GetOperatorIdAsync()), (await this.GetShiftIdAsync()), id, dto);
        return Ok(ApiResponse<BillDto>.Ok(result));
    }

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveBillItem(Guid id, Guid itemId)
    {
        var result = await _billingService.RemoveBillItemAsync(GetBranchId(), (await this.GetOperatorIdAsync()), id, itemId);
        return Ok(ApiResponse<BillDto>.Ok(result));
    }

    [HttpPost("{id:guid}/request-wallet-approval")]
    public async Task<IActionResult> RequestWalletApproval(Guid id)
    {
        var bill = await _billingService.GetBillAsync(GetBranchId(), id);
        if (bill == null || bill.MemberId == null)
            return BadRequest(new { success = false, error = "Bill not found or not linked to a member." });

        if (bill.PcId == null)
            return BadRequest(new { success = false, error = "No PC associated with this bill." });

        var approvalToken = Guid.NewGuid();
        var pendingRequest = new AppleEsportsErp.Application.DTOs.Billing.PendingWalletApproval
        {
            BillId = id,
            OperatorId = await this.GetOperatorIdAsync(),
            ShiftId = await this.GetShiftIdAsync(),
            BranchId = GetBranchId(),
            Amount = bill.TotalAmount
        };

        PendingApprovals.TryAdd(approvalToken, pendingRequest);

        // Send to PC
        await _pcOverlayHub.Clients.Group($"pc:{bill.PcId}").SendAsync("ReceiveWalletApprovalRequest", new
        {
            billId = id,
            amount = bill.TotalAmount,
            approvalToken = approvalToken
        });

        // Also fallback to pcNumber if group doesn't use ID
        if (!string.IsNullOrEmpty(bill.PcNumber))
        {
            await _pcOverlayHub.Clients.Group($"pc:{bill.PcNumber}").SendAsync("ReceiveWalletApprovalRequest", new
            {
                billId = id,
                amount = bill.TotalAmount,
                approvalToken = approvalToken
            });
        }

        return Ok(new { success = true, approvalToken });
    }
}

