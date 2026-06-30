using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.PcStatus;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public PcsController(IPcStatusService pcStatusService, IUnitOfWork unitOfWork)
    {
        _pcStatusService = pcStatusService;
        _unitOfWork = unitOfWork;
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
    public async Task<IActionResult> Create([FromBody] AppleEsportsErp.Application.DTOs.Settings.CreatePcDto dto)
    {
        var exists = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .AnyAsync(p => p.BranchId == dto.BranchId && p.PcNumber == dto.PcNumber && !p.IsDeleted);

        if (exists) return BadRequest(ApiResponse<object>.Fail("PC number already exists in this branch"));

        // Find standard pricing profile for this branch
        var pricingProfile = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.PricingProfile>()
            .Query()
            .FirstOrDefaultAsync(p => p.BranchId == dto.BranchId && p.IsActive);

        var pc = new AppleEsportsErp.Domain.Entities.Pc
        {
            Id = Guid.NewGuid(),
            PcNumber = dto.PcNumber,
            PcName = dto.PcName ?? dto.PcNumber,
            BranchId = dto.BranchId,
            IpAddress = dto.IpAddress,
            Specs = dto.Specs ?? "{}",
            Zone = dto.Zone ?? "Standard",
            HardwareNotes = dto.HardwareNotes,
            PricingProfileId = pricingProfile?.Id,
            State = AppleEsportsErp.Domain.Enums.PcState.Idle,
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
    public async Task<IActionResult> Update(Guid id, [FromBody] AppleEsportsErp.Application.DTOs.Settings.UpdatePcDto dto)
    {
        var pc = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (pc == null) return NotFound(ApiResponse<object>.Fail("PC not found"));

        var exists = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .AnyAsync(p => p.BranchId == pc.BranchId && p.PcNumber == dto.PcNumber && p.Id != id && !p.IsDeleted);

        if (exists) return BadRequest(ApiResponse<object>.Fail("PC number already exists in this branch"));

        pc.PcNumber = dto.PcNumber;
        pc.PcName = dto.PcName ?? dto.PcNumber;
        pc.IpAddress = dto.IpAddress;
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
    public async Task<IActionResult> Delete(Guid id)
    {
        var pc = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>()
            .Query()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (pc == null) return NotFound(ApiResponse<object>.Fail("PC not found"));

        pc.IsDeleted = true;
        pc.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>().Update(pc);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { message = "PC deleted successfully" }));
    }
}

