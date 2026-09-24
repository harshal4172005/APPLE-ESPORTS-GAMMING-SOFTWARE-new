using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Api.Services;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.PcStatus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/pcs")]
[Authorize]
[BranchIsolation]
public class PcsController : ControllerBase
{
    private readonly IPcStatusService _pcStatusService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRemoteBranchControl _remote;

    public PcsController(IPcStatusService pcStatusService, IUnitOfWork unitOfWork, IRemoteBranchControl remote)
    {
        _pcStatusService = pcStatusService;
        _unitOfWork = unitOfWork;
        _remote = remote;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var branchId = Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);
        var pcs = await _pcStatusService.GetBranchPcStatusesAsync(branchId);
        
        return Ok(ApiResponse<IEnumerable<PcStatusDto>>.Ok(pcs));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var pc = await _pcStatusService.GetPcStatusAsync(id);
        return Ok(ApiResponse<PcStatusDto>.Ok(pc));
    }

    /// <summary>Full PC details for Settings page PC fleet management</summary>
    [HttpGet("details")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> GetDetails([FromQuery] Guid? branchId = null)
    {
        var targetBranchId = branchId;
        if (targetBranchId == null && HttpContext.Items.TryGetValue("BranchId", out var itemVal) && itemVal != null)
        {
            targetBranchId = Guid.Parse(itemVal.ToString()!);
        }
        if (targetBranchId == null)
            return BadRequest(ApiResponse<object>.Fail("Branch context required."));

        var pcs = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .Where(p => p.BranchId == targetBranchId.Value && !p.IsDeleted)
            .OrderBy(p => p.PcNumber)
            .ToListAsync();

        var dtos = pcs.Select(p => new {
            p.Id,
            p.PcNumber,
            p.PcName,
            p.BranchId,
            p.IpAddress,
            p.Specs,
            p.Zone,
            p.HardwareNotes,
            State = p.State.ToString(),
            p.IsActive,
            p.CreatedAt,
            p.UpdatedAt
        });

        return Ok(ApiResponse<object>.Ok(dtos));
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Create([FromBody] AppleEsportsErp.Application.DTOs.Settings.CreatePcDto dto, CancellationToken ct)
    {
        var exists = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .AnyAsync(p => p.BranchId == dto.BranchId && p.PcNumber == dto.PcNumber && !p.IsDeleted);

        if (exists) return BadRequest(ApiResponse<object>.Fail("PC number already exists in this branch"));

        // A PC must have a Pricing Profile — either the one explicitly chosen, or (if none
        // was chosen) this branch's only/first active profile as a convenience default.
        AppleEsportsErp.Domain.Entities.PricingProfile? pricingProfile = null;
        if (dto.PricingProfileId.HasValue)
        {
            pricingProfile = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.PricingProfile>()
                .Query()
                .FirstOrDefaultAsync(p => p.Id == dto.PricingProfileId.Value && p.BranchId == dto.BranchId);
            if (pricingProfile == null)
                return BadRequest(ApiResponse<object>.Fail("Invalid or inaccessible Pricing Profile."));
        }
        else
        {
            pricingProfile = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.PricingProfile>()
                .Query()
                .FirstOrDefaultAsync(p => p.BranchId == dto.BranchId && p.IsActive);
        }

        if (pricingProfile == null)
            return BadRequest(ApiResponse<object>.Fail("This branch has no Pricing Profile yet. Create one in Settings → Pricing Profiles before adding a PC."));

        // A console (PS5, Xbox, etc.) has no ClientAgent to ever call /api/agent/provision -
        // there is no software running on it at all, only a row in this table used for timing
        // and billing. Gating it behind AwaitingSetup the same way a real gaming PC is would
        // mean it could never be started on, forever - nothing would ever flip it to Idle. It
        // is trusted to be ready the moment Super Admin registers it instead.
        var isConsole = string.Equals(dto.Zone, "Console", StringComparison.OrdinalIgnoreCase);

        // Head Office holds a mirrored copy of every branch's PCs, not the real one - writing a
        // new row here the way this endpoint used to and stopping there would create it only in
        // that mirror, never on the branch's own database the counter and the gaming PCs
        // actually read from. It looked like it worked (Head Office's own Settings page showed
        // the new PC immediately) and did nothing an operator at the branch could ever see, the
        // same fault RemoteBranchControl exists to fix for every other PC action.
        if (_remote.MustTravel)
        {
            // Created here too, not only queued - a row that only ever existed as a pending
            // command had no identity for a heartbeat to ever match against. The branch's own
            // ApplyPcStatesAsync only updates a PcId it already recognises; it can never create
            // one. Without this, the id the branch eventually generates for its own local row
            // was one Head Office had never seen, so every heartbeat reporting it was silently
            // ignored forever - exactly how a PS5 added from Head Office landed only in Head
            // Office's own database and never reached the branch at all, found live on Testing.
            //
            // AwaitingSetup regardless of zone - even a console, which the local branch below
            // defaults straight to Idle - because nothing here is confirmed real yet; the
            // branch has not answered. Whatever it actually decides arrives on its own very next
            // heartbeat and corrects this the normal way, the same as any other PC's state does.
            var mirror = new AppleEsportsErp.Domain.Entities.Pc
            {
                Id = Guid.NewGuid(),
                PcNumber = dto.PcNumber,
                PcName = dto.PcName ?? dto.PcNumber,
                BranchId = dto.BranchId,
                IpAddress = isConsole ? null : dto.IpAddress,
                Specs = dto.Specs ?? "{}",
                Zone = dto.Zone ?? "Standard",
                HardwareNotes = dto.HardwareNotes,
                PricingProfileId = pricingProfile?.Id,
                State = AppleEsportsErp.Domain.Enums.PcState.AwaitingSetup,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>().AddAsync(mirror);
            await _unitOfWork.SaveChangesAsync();

            dto.Id = mirror.Id;

            var receipt = await _remote.SendAsync(
                dto.BranchId, BranchCommands.AddPc, dto,
                Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);

            return Accepted(ApiResponse<object>.Ok(new
            {
                queued = true,
                commandId = receipt.CommandId,
                branchIsReporting = receipt.BranchIsReporting,
                message = receipt.Message,
                mirror.Id,
                mirror.PcNumber,
            }));
        }

        var pc = new AppleEsportsErp.Domain.Entities.Pc
        {
            Id = Guid.NewGuid(),
            PcNumber = dto.PcNumber,
            PcName = dto.PcName ?? dto.PcNumber,
            BranchId = dto.BranchId,
            IpAddress = isConsole ? null : dto.IpAddress,
            Specs = dto.Specs ?? "{}",
            Zone = dto.Zone ?? "Standard",
            HardwareNotes = dto.HardwareNotes,
            PricingProfileId = pricingProfile?.Id,

            // NOT Idle for a real gaming PC. This is the record's first moment of existing - no
            // physical machine has claimed it yet, and Idle would tell the Sessions page (and the
            // public walk-in kiosk picker, PublicController's PcState.Idle filter) that it is a
            // real, bookable seat. It was Idle here for as long as this endpoint has existed,
            // which is why a brand-new, never-set-up PC looked identical to a genuinely free one -
            // both blue, both "FREE" - until whichever machine claims this PC number calls
            // /api/agent/provision and that flips it to Idle for real. See PcState.AwaitingSetup.
            // A console skips this entirely - see isConsole above.
            State = isConsole ? AppleEsportsErp.Domain.Enums.PcState.Idle : AppleEsportsErp.Domain.Enums.PcState.AwaitingSetup,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>().AddAsync(pc);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { pc.Id, pc.PcNumber }));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AppleEsportsErp.Application.DTOs.Settings.UpdatePcDto dto, CancellationToken ct)
    {
        var pc = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (pc == null) return NotFound(ApiResponse<object>.Fail("PC not found"));

        // Same fault Create had until it was fixed: this endpoint only ever wrote to whichever
        // database the caller is connected to. From Head Office that is its own mirror only -
        // a PC edited there kept showing the old name/zone/IP at the branch forever, since
        // nothing about a PC ever travelled down the config-sync channel (that carries
        // operators, menu items and members, never PCs).
        if (_remote.MustTravel)
        {
            var receipt = await _remote.SendAsync(
                pc.BranchId, BranchCommands.UpdatePc,
                new { Id = id, dto.PcNumber, dto.PcName, dto.IpAddress, dto.Specs, dto.Zone, dto.HardwareNotes },
                Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);

            return Accepted(ApiResponse<object>.Ok(new
            {
                queued = true,
                commandId = receipt.CommandId,
                branchIsReporting = receipt.BranchIsReporting,
                message = receipt.Message,
            }));
        }

        var exists = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .AnyAsync(p => p.BranchId == pc.BranchId && p.PcNumber == dto.PcNumber && p.Id != id && !p.IsDeleted);

        if (exists) return BadRequest(ApiResponse<object>.Fail("PC number already exists in this branch"));

        var isConsole = string.Equals(dto.Zone, "Console", StringComparison.OrdinalIgnoreCase);

        pc.PcNumber = dto.PcNumber;
        pc.PcName = dto.PcName ?? dto.PcNumber;
        pc.IpAddress = isConsole ? null : dto.IpAddress;
        pc.Specs = dto.Specs ?? "{}";
        pc.Zone = dto.Zone ?? "Standard";
        pc.HardwareNotes = dto.HardwareNotes;
        pc.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>().Update(pc);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { pc.Id, pc.PcNumber }));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var pc = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (pc == null) return NotFound(ApiResponse<object>.Fail("PC not found"));

        // Same reasoning as Update above: deleting here only ever touched Head Office's own
        // mirror. This is the confirmed cause of a branch reporting far more PCs than are
        // physically real - Head Office's own list had been cleaned up, the branch's local
        // copy never heard about it, and kept counting every one of the old rows in its own
        // heartbeat forever.
        if (_remote.MustTravel)
        {
            var receipt = await _remote.SendAsync(
                pc.BranchId, BranchCommands.DeletePc, new { Id = id },
                Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);

            return Accepted(ApiResponse<object>.Ok(new
            {
                queued = true,
                commandId = receipt.CommandId,
                branchIsReporting = receipt.BranchIsReporting,
                message = receipt.Message,
            }));
        }

        pc.IsDeleted = true;
        pc.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>().Update(pc);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { message = "PC deleted successfully" }));
    }
}

