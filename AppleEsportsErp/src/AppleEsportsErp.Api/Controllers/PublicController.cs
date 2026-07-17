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
            pc = await _db.Pcs.Include(p => p.Branch).FirstOrDefaultAsync(p => p.Id == pcGuid);
        }
        else
        {
            pc = await _db.Pcs.Include(p => p.Branch).FirstOrDefaultAsync(p => p.PcNumber == pcIdentifier || p.PcName == pcIdentifier);
        }

        if (pc == null)
            return Ok(new { success = false, error = "PC not found" });

        return Ok(ApiResponse<object>.Ok(new { id = pc.Id, name = pc.PcName ?? pc.PcNumber, branchId = pc.BranchId, branchName = pc.Branch?.Name, monitorHz = pc.MonitorHz }));
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
            pc = await _db.Pcs.Include(p => p.Branch).FirstOrDefaultAsync(p => p.Id == pcGuid);
        }
        else
        {
            pc = await _db.Pcs.Include(p => p.Branch).FirstOrDefaultAsync(p => p.PcNumber == pcId || p.PcName == pcId);
        }

        if (pc == null)
            return Ok(new { success = false, error = "PC not found" });

        decimal ratePerHour = GetRateForBranchAndTier(pc.Branch?.Name ?? "", pc.MonitorHz ?? "");

        var plans = new List<object>();
        string planName = string.IsNullOrEmpty(pc.MonitorHz) ? "Standard" : $"{pc.MonitorHz}Hz Tier";

        plans.Add(new { id = Guid.NewGuid(), name = $"1 Hour ({planName})", duration = 60, price = ratePerHour, isPostpaid = false });
        plans.Add(new { id = Guid.NewGuid(), name = $"2 Hours ({planName})", duration = 120, price = ratePerHour * 2, isPostpaid = false });
        plans.Add(new { id = Guid.NewGuid(), name = $"3 Hours ({planName})", duration = 180, price = ratePerHour * 3, isPostpaid = false });
        plans.Add(new { id = Guid.NewGuid(), name = $"Postpaid ({planName})", duration = 0, price = 0m, isPostpaid = true });

        return Ok(ApiResponse<object>.Ok(plans));
    }

    private decimal GetRateForBranchAndTier(string branchName, string monitorHz)
    {
        branchName = branchName?.Trim().ToLowerInvariant() ?? "";
        monitorHz = monitorHz?.Trim().ToLowerInvariant() ?? "";

        if (branchName.Contains("adajan"))
        {
            return 60m; // 240Hz only
        }
        if (branchName.Contains("citylight"))
        {
            if (monitorHz == "144hz") return 50m;
            if (monitorHz == "240hz") return 60m;
            return 50m; // default
        }
        if (branchName.Contains("katargam"))
        {
            if (monitorHz == "165hz") return 60m;
            if (monitorHz == "240hz") return 70m;
            if (monitorHz == "360hz") return 80m;
            return 60m; // default
        }
        if (branchName.Contains("varachha"))
        {
            if (monitorHz == "240hz") return 80m;
            if (monitorHz == "400hz") return 90m;
            return 80m; // default
        }

        return 100m; // fallback
    }

    [HttpGet("branches/{branchId}/plans")]
    public async Task<IActionResult> GetBranchPlans(Guid branchId)
    {
        var branch = await _db.Branches
            .Include(b => b.Pcs)
            .FirstOrDefaultAsync(b => b.Id == branchId);

        if (branch == null)
            return Ok(new { success = false, error = "Branch not found" });

        var uniqueTiers = branch.Pcs
            .Where(p => p.IsActive && !p.IsDeleted)
            .Select(p => string.IsNullOrWhiteSpace(p.MonitorHz) ? "Standard" : p.MonitorHz)
            .Distinct()
            .ToList();

        if (!uniqueTiers.Any())
        {
            uniqueTiers.Add("Standard");
        }

        var plans = new List<object>();

        foreach (var tier in uniqueTiers)
        {
            decimal ratePerHour = GetRateForBranchAndTier(branch.Name, tier);
            string planNameTier = tier == "Standard" ? "Standard Tier" : $"{tier}Hz Tier";
            string monitorHzVal = tier == "Standard" ? "" : tier;

            plans.Add(new { id = Guid.NewGuid(), name = $"1 Hour ({planNameTier})", duration = 60, price = ratePerHour, tier = monitorHzVal, tierLabel = planNameTier, isPostpaid = false });
            plans.Add(new { id = Guid.NewGuid(), name = $"2 Hours ({planNameTier})", duration = 120, price = ratePerHour * 2, tier = monitorHzVal, tierLabel = planNameTier, isPostpaid = false });
            plans.Add(new { id = Guid.NewGuid(), name = $"3 Hours ({planNameTier})", duration = 180, price = ratePerHour * 3, tier = monitorHzVal, tierLabel = planNameTier, isPostpaid = false });
            plans.Add(new { id = Guid.NewGuid(), name = $"Postpaid ({planNameTier})", duration = 0, price = 0m, tier = monitorHzVal, tierLabel = planNameTier, isPostpaid = true });
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
        [FromServices] ISessionService sessionService,
        [FromServices] IHubContext<PcStatusHub> pcStatusHub,
        [FromServices] IHubContext<SessionHub> sessionHub)
    {
        // Get the MemberId from the JWT Token
        var memberIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(memberIdStr) || !Guid.TryParse(memberIdStr, out var memberId))
            return Unauthorized(new { success = false, error = "Invalid Member token." });

        dto.MemberId = memberId;

        // Retrieve PC to find out which Branch this session belongs to
        var pc = await _db.Pcs.Include(p => p.Branch).FirstOrDefaultAsync(p => p.Id == dto.PcId);
        if (pc == null)
            return BadRequest(new { success = false, error = "PC not found." });

        var branchId = pc.BranchId;

        // ── AUTO-START FROM RESERVATION (SOP §22) ──
        // If a pending reservation exists for this (memberId, pcId) within the time window,
        // auto-start the session from it — no operator approval needed.
        var now = DateTimeOffset.UtcNow;
        var pendingReservation = await _db.Reservations
            .Where(r => r.MemberId == memberId
                     && r.PcId == dto.PcId
                     && r.State == ReservationState.Pending
                     && r.ReservationTime.AddMinutes(-30) <= now
                     && r.ReservationTime.AddMinutes(r.GracePeriodMin) >= now)
            .OrderBy(r => Math.Abs((r.ReservationTime - now).TotalMinutes))
            .FirstOrDefaultAsync();

        if (pendingReservation != null)
        {
            // Retrieve or create System Operator and Shift
            var sysUsername2 = $"system_admin_{branchId:N}";
            var sysOp2 = await _db.Operators.FirstOrDefaultAsync(o => o.BranchId == branchId && o.Username == sysUsername2);
            if (sysOp2 == null)
            {
                sysOp2 = new Operator { Id = Guid.NewGuid(), BranchId = branchId, FullName = "System Administrator", Username = sysUsername2, PasswordHash = "LOCKED", Status = OperatorStatus.Active, CreatedAt = now, UpdatedAt = now };
                _db.Operators.Add(sysOp2);
                await _db.SaveChangesAsync();
            }
            var sysShift2 = await _db.Shifts.FirstOrDefaultAsync(s => s.BranchId == branchId && s.Status == ShiftStatus.Active && s.OperatorId == sysOp2.Id);
            if (sysShift2 == null)
            {
                sysShift2 = new Shift { Id = Guid.NewGuid(), BranchId = branchId, OperatorId = sysOp2.Id, LoginTime = now, CreatedAt = now, Status = ShiftStatus.Active };
                _db.Shifts.Add(sysShift2);
                _db.CashRegisters.Add(new CashRegister { Id = Guid.NewGuid(), BranchId = branchId, OperatorId = sysOp2.Id, ShiftId = sysShift2.Id, OpeningBalance = 0, ExpectedDrawerCash = 0, TotalCashSales = 0, TotalSplitCash = 0, Status = CashRegisterStatus.Open, OpenedAt = now });
                await _db.SaveChangesAsync();
            }

            // Build session from reservation
            var durationMin = pendingReservation.DurationMin ?? 60;
            var ratePerHour = GetRateForBranchAndTier(pc.Branch?.Name ?? "", pc.MonitorHz ?? "");
            var expectedAmount = pendingReservation.DurationMin.HasValue ? (durationMin / 60m) * ratePerHour : 0m; // 0 for member-only (postpaid)

            var session = new Session
            {
                Id = Guid.NewGuid(), PcId = pc.Id, BranchId = branchId,
                OperatorId = sysOp2.Id, ShiftId = sysShift2.Id,
                CustomerName = pendingReservation.CustomerName, MemberId = memberId,
                StartTime = now, EndTime = pendingReservation.DurationMin.HasValue ? now.AddMinutes(durationMin) : (DateTimeOffset?)null,
                PlannedDurationMin = pendingReservation.DurationMin,
                TotalAmount = expectedAmount, GamingAmount = expectedAmount,
                GamingType = "Member Reservation Auto-Start",
                State = SessionState.Active, Notes = pendingReservation.Notes,
                CreatedAt = now, UpdatedAt = now
            };
            _db.Sessions.Add(session);

            var totalDue = Math.Max(0m, expectedAmount - pendingReservation.AdvanceDeposit);
            var bill = new Bill
            {
                Id = Guid.NewGuid(),
                BillNumber = $"BILL-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                SessionId = session.Id, PcId = pc.Id, BranchId = branchId,
                OperatorId = sysOp2.Id, ShiftId = sysShift2.Id,
                CustomerName = pendingReservation.CustomerName, MemberId = memberId,
                GamingAmount = expectedAmount, FoodAmount = 0, Subtotal = expectedAmount, TotalAmount = totalDue,
                Status = BillStatus.Pending, CreatedAt = now, UpdatedAt = now,
                DiscountReason = pendingReservation.AdvanceDeposit > 0 ? $"Advance deposit ₹{pendingReservation.AdvanceDeposit} applied" : null
            };
            _db.Bills.Add(bill);

            pendingReservation.State = ReservationState.Completed;
            pendingReservation.StartedAt = now;
            _db.Reservations.Update(pendingReservation);

            pc.State = PcState.Active;
            pc.CurrentSessionId = session.Id;
            pc.CurrentReservationId = pendingReservation.Id;
            _db.Pcs.Update(pc);

            await _db.SaveChangesAsync();

            // Broadcast updates
            await pcStatusHub.Clients.Group($"branch:{branchId}").SendAsync("PcStatusChanged", new { pcId = pc.Id, state = "Active" });
            await sessionHub.Clients.Group($"branch:{branchId}").SendAsync("SessionUpdated", new { sessionId = session.Id });

            return Ok(ApiResponse<object>.Ok(new { sessionId = session.Id, pcId = pc.Id, pcName = pc.PcName ?? pc.PcNumber, startTime = session.StartTime, fromReservation = true }));
        }
        // ── END AUTO-START FROM RESERVATION ──

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
