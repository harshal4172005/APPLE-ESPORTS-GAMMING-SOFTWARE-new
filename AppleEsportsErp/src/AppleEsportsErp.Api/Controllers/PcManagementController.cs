using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.PcManagement;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Constants;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/pc-management")]
[Authorize] // Methods restricted individually
public class PcManagementController : ControllerBase
{
    private readonly IPcManagementService _pcManagementService;

    public PcManagementController(IPcManagementService pcManagementService)
    {
        _pcManagementService = pcManagementService;
    }

    private Guid GetSuperAdminId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("branch/{branchId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Admin)]
    public async Task<IActionResult> GetPcsByBranch(Guid branchId, [FromQuery] bool includeDeleted = false)
    {
        var result = await _pcManagementService.GetPcsByBranchAsync(branchId, includeDeleted);
        return Ok(ApiResponse<List<PcDto>>.Ok(result));
    }

    [HttpPost("branch/{branchId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Admin)]
    public async Task<IActionResult> AddPc(Guid branchId, [FromBody] CreatePcDto dto)
    {
        var result = await _pcManagementService.AddPcAsync(branchId, GetSuperAdminId(), dto);
        return Ok(ApiResponse<PcDto>.Ok(result));
    }

    [HttpPut("{pcId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Admin)]
    public async Task<IActionResult> UpdatePc(Guid pcId, [FromBody] UpdatePcDto dto)
    {
        var result = await _pcManagementService.UpdatePcAsync(pcId, GetSuperAdminId(), dto);
        return Ok(ApiResponse<PcDto>.Ok(result));
    }

    [HttpPost("{pcId:guid}/transfer/{newBranchId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Admin)]
    public async Task<IActionResult> TransferPc(Guid pcId, Guid newBranchId)
    {
        var result = await _pcManagementService.TransferPcAsync(pcId, newBranchId, GetSuperAdminId());
        return Ok(ApiResponse<PcDto>.Ok(result));
    }

    [HttpPost("{pcId:guid}/maintenance")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Admin + "," + Roles.Operator)]
    public async Task<IActionResult> MarkMaintenance(Guid pcId, [FromQuery] bool enable)
    {
        var result = await _pcManagementService.MarkMaintenanceAsync(pcId, GetSuperAdminId(), enable);
        return Ok(ApiResponse<PcDto>.Ok(result));
    }

    [HttpDelete("{pcId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Admin)]
    public async Task<IActionResult> DeletePc(Guid pcId)
    {
        await _pcManagementService.DeletePcAsync(pcId, GetSuperAdminId());
        return Ok(ApiResponse.Ok());
    }
}

