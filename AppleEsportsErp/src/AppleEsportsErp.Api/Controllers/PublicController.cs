using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Sessions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using AppleEsportsErp.Api.Controllers; // To access BillingController.PendingApprovals

namespace AppleEsportsErp.Api.Controllers;

/// <summary>
/// Unauthenticated public endpoints used by the user panel (kiosk, walk-in, member portal, PC overlay).
/// No JWT required — branch isolation is handled by explicit branchId parameters.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List all active branches — shown in walk-in & member branch selection screens.</summary>
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var branches = await _db.Branches
            .Where(b => b.Status == BranchStatus.Active)
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name, b.Address })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(branches));
    }

    /// <summary>List idle PCs for a branch — shown in member portal PC selection screen.</summary>
    [HttpGet("branches/{branchId:guid}/pcs/idle")]
    public async Task<IActionResult> GetIdlePcs(Guid branchId)
    {
        var pcs = await _db.Pcs
            .Where(p => p.BranchId == branchId && p.State == PcState.Idle)
            .OrderBy(p => p.PcNumber)
            .Select(p => new { p.Id, Name = p.PcName ?? p.PcNumber })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(pcs));
    }

    /// <summary>List all PCs for a branch — used by setup screen.</summary>
    [HttpGet("branches/{branchId:guid}/pcs")]
    public async Task<IActionResult> GetPcsForBranch(Guid branchId)
    {
        var pcs = await _db.Pcs
            .Where(p => p.BranchId == branchId)
            .OrderBy(p => p.PcNumber)
            .Select(p => new { p.Id, p.PcNumber, p.PcName, Name = p.PcName ?? p.PcNumber })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(pcs));
    }

    /// <summary>
    /// Get the active session for a PC — used by the overlay on startup to load real session data.
    /// Accepts either a PC UUID or a PC name string (e.g. "PC-07").
    /// </summary>
    [HttpGet("session/pc/{pcIdentifier}")]
    public async Task<IActionResult> GetSessionByPc(string pcIdentifier)
    {
        // Try UUID first, fall back to name lookup
        Domain.Entities.Pc? pc = null;
        if (Guid.TryParse(pcIdentifier, out var pcGuid))
        {
            pc = await _db.Pcs.Include(p => p.PricingProfile).FirstOrDefaultAsync(p => p.Id == pcGuid);
        }
        else
        {
            pc = await _db.Pcs.Include(p => p.PricingProfile).FirstOrDefaultAsync(p => p.PcNumber == pcIdentifier || p.PcName == pcIdentifier);
        }

        if (pc == null)
            return Ok(new { success = true, data = (object?)null });

        var session = await _db.Sessions
            .Include(s => s.FoodOrders).ThenInclude(fo => fo.Items)
            .Where(s => s.PcId == pc.Id && s.State == SessionState.Active)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (session == null)
            return Ok(new { success = true, data = (object?)null });

        var foodCharges = session.FoodOrders.Sum(fo => fo.TotalAmount);
        var elapsedMinutes = (DateTimeOffset.UtcNow - session.StartTime).TotalMinutes;
        var remainingSeconds = session.PlannedDurationMin.HasValue
            ? Math.Max(0, (session.PlannedDurationMin.Value - elapsedMinutes) * 60)
            : null as double?;

        var globalConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == "global_system_rules");
        decimal defaultBaseRate = 100m;
        Dictionary<string, decimal> hzPricing = new();
        if (globalConfig != null && !string.IsNullOrEmpty(globalConfig.ConfigValue))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(globalConfig.ConfigValue);
                if (doc.RootElement.TryGetProperty("pricing", out var pricing))
                {
                    if (pricing.TryGetProperty("baseRate", out var br))
                        defaultBaseRate = br.GetDecimal();
                        
                    if (pricing.TryGetProperty("hzPricing", out var hzObj))
                    {
                        foreach (var prop in hzObj.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                                hzPricing[prop.Name] = prop.Value.GetDecimal();
                        }
                    }
                }
            }
            catch { /* fallback */ }
        }

        decimal ratePerHour = defaultBaseRate;
        if (!string.IsNullOrEmpty(pc.MonitorHz) && hzPricing.TryGetValue(pc.MonitorHz, out var hrRate))
        {
            ratePerHour = hrRate;
        }

        decimal? walletBalance = null;
        decimal? gamingBalance = null;
        decimal? foodBalance = null;
        if (session.MemberId.HasValue)
        {
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == session.MemberId.Value);
            if (member != null)
            {
                walletBalance = member.GamingBalance + member.FoodBalance;
                gamingBalance = member.GamingBalance;
                foodBalance = member.FoodBalance;
            }
        }

        var result = new
        {
            sessionId = session.Id.ToString(),
            pcId = pc.Id.ToString(),
            pcName = pc.PcName ?? pc.PcNumber,
            monitorHz = pc.MonitorHz,
            customerName = session.CustomerName ?? "Guest",
            sessionStart = session.StartTime,
            remainingTime = remainingSeconds.HasValue ? (int)remainingSeconds.Value : (int?)null,
            plannedDurationMin = session.PlannedDurationMin,
            ratePerHour = ratePerHour,
            gamingCharges = session.GamingAmount,
            foodCharges,
            foodItems = session.FoodOrders
                .SelectMany(fo => fo.Items)
                .Select(i => new { i.ItemName, i.Quantity, i.UnitPrice })
                .ToList(),
            totalBill = session.GamingAmount + foodCharges,
            sessionStatus = session.State.ToString().ToLowerInvariant(),
            memberId = session.MemberId?.ToString(),
            walletBalance = walletBalance,
            gamingBalance = gamingBalance,
            foodBalance = foodBalance
        };

        return Ok(ApiResponse<object>.Ok(result));
    }
    [HttpGet("pcs/{pcIdentifier}")]
    public async Task<IActionResult> GetPc(string pcIdentifier)
    {
        Domain.Entities.Pc? pc = null;
        if (Guid.TryParse(pcIdentifier, out var pcGuid))
        {
            pc = await _db.Pcs.FirstOrDefaultAsync(p => p.Id == pcGuid);
        }
        else
        {
            pc = await _db.Pcs.FirstOrDefaultAsync(p => p.PcNumber == pcIdentifier || p.PcName == pcIdentifier);
        }

        if (pc == null)
            return Ok(new { success = false, error = "PC not found" });

        return Ok(ApiResponse<object>.Ok(new { id = pc.Id, name = pc.PcName ?? pc.PcNumber, branchId = pc.BranchId, monitorHz = pc.MonitorHz }));
    }

    [HttpPost("pcs/{pcId:guid}/hz")]
    public async Task<IActionResult> SetPcMonitorHz(Guid pcId, [FromBody] SetMonitorHzDto dto)
    {
        var pc = await _db.Pcs.FirstOrDefaultAsync(p => p.Id == pcId);
        if (pc == null)
            return Ok(new { success = false, error = "PC not found" });

        pc.MonitorHz = dto.MonitorHz;
        pc.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { success = true }));
    }

    [HttpGet("pcs/{pcId}/plans")]
    public async Task<IActionResult> GetPcPlans(string pcId)
    {
        Domain.Entities.Pc? pc = null;
        if (Guid.TryParse(pcId, out var pcGuid))
        {
            pc = await _db.Pcs.FirstOrDefaultAsync(p => p.Id == pcGuid);
        }
        else
        {
            pc = await _db.Pcs.FirstOrDefaultAsync(p => p.PcNumber == pcId || p.PcName == pcId);
        }

        if (pc == null)
            return Ok(new { success = false, error = "PC not found" });

        var globalConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == "global_system_rules");
        decimal defaultBaseRate = 100m;
        Dictionary<string, decimal> hzPricing = new();
        if (globalConfig != null && !string.IsNullOrEmpty(globalConfig.ConfigValue))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(globalConfig.ConfigValue);
                if (doc.RootElement.TryGetProperty("pricing", out var pricing))
                {
                    if (pricing.TryGetProperty("baseRate", out var br))
                        defaultBaseRate = br.GetDecimal();
                        
                    if (pricing.TryGetProperty("hzPricing", out var hzObj))
                    {
                        foreach (var prop in hzObj.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                                hzPricing[prop.Name] = prop.Value.GetDecimal();
                        }
                    }
                }
            }
            catch { /* fallback */ }
        }

        decimal ratePerHour = defaultBaseRate;
        if (!string.IsNullOrEmpty(pc.MonitorHz) && hzPricing.TryGetValue(pc.MonitorHz, out var hrRate))
        {
            ratePerHour = hrRate;
        }

        var plans = new List<object>();
        string planName = string.IsNullOrEmpty(pc.MonitorHz) ? "Standard" : $"{pc.MonitorHz}Hz Tier";

        plans.Add(new { id = Guid.NewGuid(), name = $"1 Hour ({planName})", duration = 60, price = ratePerHour, isPostpaid = false });
        plans.Add(new { id = Guid.NewGuid(), name = $"2 Hours ({planName})", duration = 120, price = ratePerHour * 2, isPostpaid = false });
        plans.Add(new { id = Guid.NewGuid(), name = $"3 Hours ({planName})", duration = 180, price = ratePerHour * 3, isPostpaid = false });
        plans.Add(new { id = Guid.NewGuid(), name = $"Postpaid ({planName})", duration = 0, price = 0, isPostpaid = true });

        return Ok(ApiResponse<object>.Ok(plans));
    }

    [HttpGet("branches/{branchId}/plans")]
    public async Task<IActionResult> GetBranchPlans(Guid branchId)
    {
        var branch = await _db.Branches
            .Include(b => b.Pcs)
            .FirstOrDefaultAsync(b => b.Id == branchId);

        if (branch == null)
            return Ok(new { success = false, error = "Branch not found" });

        var globalConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == "global_system_rules");
        decimal defaultBaseRate = 100m;
        Dictionary<string, decimal> hzPricing = new();
        if (globalConfig != null && !string.IsNullOrEmpty(globalConfig.ConfigValue))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(globalConfig.ConfigValue);
                if (doc.RootElement.TryGetProperty("pricing", out var pricing))
                {
                    if (pricing.TryGetProperty("baseRate", out var br))
                        defaultBaseRate = br.GetDecimal();
                        
                    if (pricing.TryGetProperty("hzPricing", out var hzObj))
                    {
                        foreach (var prop in hzObj.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                                hzPricing[prop.Name] = prop.Value.GetDecimal();
                        }
                    }
                }
            }
            catch { /* fallback */ }
        }

        // Determine unique tiers in the branch
        var uniqueTiers = branch.Pcs
            .Where(p => p.IsActive && !p.IsDeleted)
            .Select(p => string.IsNullOrWhiteSpace(p.MonitorHz) ? "Standard" : p.MonitorHz)
            .Distinct()
            .ToList();

        if (!uniqueTiers.Any())
        {
            uniqueTiers.Add("Standard");
        }

        // Parse branch durations
        List<int> durations = new List<int> { 30, 60, 120, 180, 240, 360, 480 };
        if (!string.IsNullOrEmpty(branch.ConfiguredReservationDurations))
        {
            var parsed = branch.ConfiguredReservationDurations.Split(',')
                .Select(d => int.TryParse(d.Trim(), out var val) ? val : 0)
                .Where(v => v > 0)
                .ToList();
            if (parsed.Any()) durations = parsed;
        }

        var plans = new List<object>();

        foreach (var tier in uniqueTiers)
        {
            decimal ratePerHour = defaultBaseRate;
            string planNameTier = tier == "Standard" ? "Standard Tier" : $"{tier}Hz Tier";
            string monitorHzVal = tier == "Standard" ? "" : tier;

            if (tier != "Standard" && hzPricing.TryGetValue(tier, out var hrRate))
            {
                ratePerHour = hrRate;
            }

            foreach (var d in durations)
            {
                decimal price = ratePerHour * (d / 60m);
                string dLabel = d < 60 ? $"{d} Mins" : d % 60 == 0 ? $"{d / 60} Hour{(d / 60 > 1 ? "s" : "")}" : $"{d / 60}h {d % 60}m";
                
                plans.Add(new {
                    id = Guid.NewGuid(),
                    name = $"{dLabel} ({planNameTier})",
                    duration = d,
                    price = price,
                    tier = monitorHzVal,
                    tierLabel = planNameTier
                });
            }
        }

        return Ok(ApiResponse<object>.Ok(plans));
    }

    /// <summary>Returns all currently pending walk-in requests. Operator polls this as a SignalR fallback.</summary>
    [HttpGet("walkin-pending")]
    [Authorize]
    public IActionResult GetPendingWalkinRequests()
    {
        var pending = PcOverlayHub.PendingWalkinRequests.Values.ToList();
        return Ok(new { success = true, data = pending });
    }

    [HttpPost("pcs/{pcId}/decline-walkin")]
    [Authorize]
    public async Task<IActionResult> DeclineWalkinRequest(string pcId, [FromServices] IHubContext<PcOverlayHub> overlayHub)
    {
        PcOverlayHub.PendingWalkinRequests.TryRemove(pcId, out _);
        await overlayHub.Clients.Group($"pc:{pcId}").SendAsync("WalkinRequestDeclined", new { reason = "Operator declined the request" });
        return Ok(ApiResponse<object>.Ok(null));
    }

    [HttpPost("sessions/member-start")]
    [Authorize] // Requires valid MemberToken
    public async Task<IActionResult> StartMemberSession(
        [FromBody] SessionStartDto dto,
        [FromServices] ISessionService sessionService)
    {
        // Get the MemberId from the JWT Token
        var memberIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberIdStr) || !Guid.TryParse(memberIdStr, out var memberId))
            return Unauthorized(new { success = false, error = "Invalid Member token." });

        dto.MemberId = memberId;

        // Retrieve PC to find out which Branch this session belongs to
        var pc = await _db.Pcs.FirstOrDefaultAsync(p => p.Id == dto.PcId);
        if (pc == null)
            return BadRequest(new { success = false, error = "PC not found." });

        var branchId = pc.BranchId;

        // Retrieve or create System Operator and Shift for this branch
        var sysUsername = $"system_admin_{branchId:N}";
        var sysOp = await _db.Operators.FirstOrDefaultAsync(o => o.BranchId == branchId && o.Username == sysUsername);
        if (sysOp == null)
        {
            sysOp = new Operator
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                FullName = "System Administrator",
                Username = sysUsername,
                PasswordHash = "LOCKED",
                Status = OperatorStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Operators.Add(sysOp);
            await _db.SaveChangesAsync();
        }

        var activeShift = await _db.Shifts.FirstOrDefaultAsync(s => s.BranchId == branchId && s.Status == ShiftStatus.Active && s.OperatorId == sysOp.Id);
        if (activeShift == null)
        {
            activeShift = new Shift
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                OperatorId = sysOp.Id,
                LoginTime = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = ShiftStatus.Active
            };
            _db.Shifts.Add(activeShift);

            var register = new CashRegister
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                OperatorId = sysOp.Id,
                ShiftId = activeShift.Id,
                OpeningBalance = 0,
                ExpectedDrawerCash = 0,
                TotalCashSales = 0,
                TotalSplitCash = 0,
                Status = CashRegisterStatus.Open,
                OpenedAt = DateTimeOffset.UtcNow
            };
            _db.CashRegisters.Add(register);
            await _db.SaveChangesAsync();
        }

        var result = await sessionService.StartSessionAsync(branchId, sysOp.Id, activeShift.Id, dto);
        return Ok(ApiResponse<SessionDto>.Ok(result));
    }

    [HttpGet("sessions/active")]
    [Authorize]
    public async Task<IActionResult> GetActiveMemberSession()
    {
        var memberIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberIdStr) || !Guid.TryParse(memberIdStr, out var memberId))
            return Unauthorized(new { success = false, error = "Invalid Member token." });

        var session = await _db.Sessions
            .Include(s => s.Pc)
            .FirstOrDefaultAsync(s => s.MemberId == memberId && s.State == AppleEsportsErp.Domain.Enums.SessionState.Active);

        if (session == null)
            return Ok(new { success = true, data = (object)null });

        return Ok(new { success = true, data = new { sessionId = session.Id, pcId = session.PcId, pcName = session.Pc?.PcName ?? session.Pc?.PcNumber, startTime = session.StartTime } });
    }

    [HttpPost("sessions/{sessionId:guid}/member-checkout")]
    [Authorize] // Requires valid MemberToken
    public async Task<IActionResult> MemberCheckout(
        Guid sessionId,
        [FromServices] ISessionService sessionService,
        [FromServices] IBillingService billingService,
        [FromServices] IHubContext<PcStatusHub> pcStatusHub,
        [FromServices] IHubContext<BillingHub> billingHub,
        [FromServices] IHubContext<SessionHub> sessionHub,
        [FromServices] IHubContext<PcOverlayHub> overlayHub)
    {
        var memberIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberIdStr) || !Guid.TryParse(memberIdStr, out var memberId))
            return Unauthorized(new { success = false, error = "Invalid Member token." });

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null || session.MemberId != memberId)
            return BadRequest(new { success = false, error = "Session not found or not owned by you." });

        var branchId = session.BranchId;
        var sysUsername = $"system_admin_{branchId:N}";
        var sysOp = await _db.Operators.FirstOrDefaultAsync(o => o.BranchId == branchId && o.Username == sysUsername);
        if (sysOp == null) return BadRequest(new { success = false, error = "System operator not found." });

        var activeShift = await _db.Shifts.FirstOrDefaultAsync(s => s.BranchId == branchId && s.Status == ShiftStatus.Active && s.OperatorId == sysOp.Id);
        var shiftId = activeShift?.Id ?? Guid.Empty;

        // 1. Stop the session to finalize bill
        var sessionResult = await sessionService.StopSessionAsync(branchId, sysOp.Id, sessionId, false);

        // 2. Fetch the bill to get the total amount
        var bill = await billingService.GetBillAsync(branchId, sessionResult.BillId);
        if (bill != null && bill.Status != AppleEsportsErp.Domain.Enums.BillStatus.Completed && bill.TotalAmount > 0)
        {
            // 3. Process wallet payment automatically
            var paymentDto = new AppleEsportsErp.Application.DTOs.Billing.ProcessPaymentDto
            {
                PaymentType = AppleEsportsErp.Domain.Enums.PaymentType.Wallet,
                CashAmount = 0,
                OnlineAmount = 0,
                WalletAmount = bill.TotalAmount,
                CashReceived = 0,
                MemberId = memberId
            };

            try
            {
                var paidBill = await billingService.ProcessPaymentAsync(branchId, sysOp.Id, shiftId, bill.Id, paymentDto);
                
                // SignalR updates for Admin dashboards
                await billingHub.Clients.Group($"branch:{branchId}").SendAsync("WalletApprovalApproved", new { billId = bill.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = "Wallet payment failed: " + ex.Message });
            }
        }

        // Broadcast PC Idle status to operator dashboard
        var pc = await _db.Pcs.FirstOrDefaultAsync(p => p.Id == sessionResult.PcId);
        if (pc != null)
        {
            var hubNotifier = HttpContext.RequestServices.GetService<AppleEsportsErp.Application.Interfaces.IHubNotificationService>();
            if (hubNotifier != null)
            {
                await hubNotifier.BroadcastPcStatusChangeAsync(branchId, pc.Id);
            }
        }

        // Notify overlay so it goes to idle screen
        await overlayHub.Clients.Group($"pc:{sessionResult.PcId}").SendAsync("PcStatusChanged", new { State = "Idle" });

        return Ok(new { success = true, data = sessionResult });
    }

    [HttpPost("bills/{billId:guid}/approve-wallet")]
    public async Task<IActionResult> ApproveWalletPayment(Guid billId, [FromBody] ApproveWalletRequest req, [FromServices] IBillingService billingService, [FromServices] IHubContext<BillingHub> billingHub)
    {
        if (!BillingController.PendingApprovals.TryGetValue(req.ApprovalToken, out var pending) || pending.BillId != billId)
            return BadRequest(new { success = false, error = "Invalid or expired approval token." });

        try
        {
            var paymentDto = new AppleEsportsErp.Application.DTOs.Billing.ProcessPaymentDto
            {
                PaymentType = AppleEsportsErp.Domain.Enums.PaymentType.Wallet,
                CashAmount = 0,
                OnlineAmount = 0,
                WalletAmount = pending.Amount,
                CashReceived = 0,
                MemberId = null // ProcessPayment links member automatically if needed, but it should already be linked
            };

            var bill = await billingService.ProcessPaymentAsync(pending.BranchId, pending.OperatorId, pending.ShiftId, billId, paymentDto);
            BillingController.PendingApprovals.TryRemove(req.ApprovalToken, out _);
            
            // Notify operator dashboard that it was approved
            await billingHub.Clients.Group($"branch:{pending.BranchId}").SendAsync("WalletApprovalApproved", new { billId });
            
            // Broadcast PC Idle status using standard hub notification service
            if (bill.PcId.HasValue)
            {
                var hubNotifier = HttpContext.RequestServices.GetService<AppleEsportsErp.Application.Interfaces.IHubNotificationService>();
                if (hubNotifier != null)
                {
                    await hubNotifier.BroadcastPcStatusChangeAsync(pending.BranchId, bill.PcId.Value);
                }

                var overlayHub = HttpContext.RequestServices.GetService<Microsoft.AspNetCore.SignalR.IHubContext<AppleEsportsErp.Api.Hubs.PcOverlayHub>>();
                if (overlayHub != null)
                {
                    await overlayHub.Clients.Group($"pc:{bill.PcId.Value}").SendAsync("PcStatusChanged", new { State = "Idle" });
                }
            }
            
            return Ok(new { success = true, data = bill });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("bills/{billId:guid}/decline-wallet")]
    public async Task<IActionResult> DeclineWalletPayment(Guid billId, [FromBody] ApproveWalletRequest req, [FromServices] IHubContext<BillingHub> billingHub)
    {
        if (!BillingController.PendingApprovals.TryGetValue(req.ApprovalToken, out var pending) || pending.BillId != billId)
            return BadRequest(new { success = false, error = "Invalid or expired approval token." });

        BillingController.PendingApprovals.TryRemove(req.ApprovalToken, out _);

        // Notify operator dashboard that it was declined
        await billingHub.Clients.Group($"branch:{pending.BranchId}").SendAsync("WalletApprovalDeclined", new { billId, reason = "Member declined the payment." });
        
        return Ok(new { success = true });
    }

    [HttpGet("branches/{branchId:guid}/menu")]
    public async Task<IActionResult> GetBranchMenu(Guid branchId)
    {
        var items = await _db.InventoryItems
            .Where(i => i.BranchId == branchId && i.Status != FoodAvailability.Disabled)
            .OrderBy(i => i.Category)
            .ThenBy(i => i.ItemName)
            .Select(i => new {
                id = i.Id,
                name = i.ItemName,
                price = i.Price,
                category = i.Category,
                inStock = i.Status != FoodAvailability.OutOfStock && i.CurrentStock > 0,
                imageUrl = i.ImageUrl
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(items));
    }
}

public class ApproveWalletRequest
{
    public Guid ApprovalToken { get; set; }
}

public class SetMonitorHzDto
{
    public string? MonitorHz { get; set; }
}
