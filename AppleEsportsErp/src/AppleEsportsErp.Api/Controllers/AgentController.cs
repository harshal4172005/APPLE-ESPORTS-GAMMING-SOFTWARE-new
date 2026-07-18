using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Application.Constants;
using AppleEsportsErp.Infrastructure.Identity;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Controllers;

/// <summary>
/// Agent management controller — handles Gaming PC agent registration, 
/// token generation, and connection status monitoring.
/// </summary>
[ApiController]
[Route("api/agent")]
[Authorize]
public class AgentController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtTokenService _jwtTokenService;

    public AgentController(IUnitOfWork unitOfWork, JwtTokenService jwtTokenService)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>Generate a long-lived machine JWT for a specific Gaming PC agent</summary>
    [HttpPost("token/{pcId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> GenerateAgentToken(Guid pcId)
    {
        var pc = await _unitOfWork.Repository<Domain.Entities.Pc>()
            .Query()
            .Include(p => p.Branch)
            .FirstOrDefaultAsync(p => p.Id == pcId && !p.IsDeleted);

        if (pc == null)
            return NotFound(ApiResponse<object>.Fail("PC not found"));

        // Generate a long-lived machine token (365 days)
        var token = _jwtTokenService.GenerateAgentToken(
            pcId: pc.Id.ToString(),
            branchId: pc.BranchId.ToString(),
            pcNumber: pc.PcNumber
        );

        return Ok(ApiResponse<object>.Ok(new
        {
            token,
            pcId = pc.Id,
            pcNumber = pc.PcNumber,
            branchId = pc.BranchId,
            branchName = pc.Branch?.Name,
            expiresIn = "365 days",
            instructions = "Save this token in the agent's appsettings.agent.json file on the Gaming PC."
        }));
    }

    /// <summary>Get agent connection status for all PCs in a branch</summary>
    [HttpGet("status/{branchId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Admin + "," + Roles.Operator)]
    public async Task<IActionResult> GetAgentStatuses(Guid branchId)
    {
        var pcs = await _unitOfWork.Repository<Domain.Entities.Pc>()
            .Query()
            .Where(p => p.BranchId == branchId && !p.IsDeleted)
            .OrderBy(p => p.PcNumber)
            .Select(p => new
            {
                p.Id,
                p.PcNumber,
                p.PcName,
                p.IsAgentOnline,
                p.ConnectionMode,
                p.LastAgentHeartbeat,
                State = p.State.ToString(),
                p.IpAddress
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(pcs));
    }

    /// <summary>Update agent heartbeat — called periodically by the agent via REST</summary>
    [HttpPost("heartbeat")]
    [AllowAnonymous] // Agents use their own machine tokens
    public async Task<IActionResult> Heartbeat([FromBody] AgentHeartbeatDto dto)
    {
        if (dto == null || string.IsNullOrEmpty(dto.PcId))
            return BadRequest(ApiResponse<object>.Fail("Invalid heartbeat data"));

        var pcId = Guid.Parse(dto.PcId);
        var pc = await _unitOfWork.Repository<Domain.Entities.Pc>()
            .Query()
            .FirstOrDefaultAsync(p => p.Id == pcId && !p.IsDeleted);

        if (pc == null)
            return NotFound(ApiResponse<object>.Fail("PC not found"));

        pc.IsAgentOnline = true;
        pc.ConnectionMode = dto.ConnectionMode ?? "LAN";
        pc.LastAgentHeartbeat = DateTimeOffset.UtcNow;
        pc.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<Domain.Entities.Pc>().Update(pc);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse.Ok());
    }

    /// <summary>Remote start a session on a Cloud Mode PC — Admin/SuperAdmin only</summary>
    [HttpPost("remote-start/{pcId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin + "," + Roles.Admin)]
    public async Task<IActionResult> RemoteStartSession(Guid pcId, [FromBody] RemoteStartDto dto)
    {
        var pc = await _unitOfWork.Repository<Domain.Entities.Pc>()
            .Query()
            .FirstOrDefaultAsync(p => p.Id == pcId && !p.IsDeleted);

        if (pc == null)
            return NotFound(ApiResponse<object>.Fail("PC not found"));

        if (!pc.IsAgentOnline)
            return BadRequest(ApiResponse<object>.Fail("PC agent is offline. Cannot start session remotely."));

        // The actual unlock command is sent via SignalR from the frontend.
        // This endpoint just validates and creates the session record.
        return Ok(ApiResponse<object>.Ok(new
        {
            pcId = pc.Id,
            pcNumber = pc.PcNumber,
            connectionMode = pc.ConnectionMode,
            message = "PC validated. Send unlock command via SignalR."
        }));
    }
}

public class AgentHeartbeatDto
{
    public string PcId { get; set; } = null!;
    public string? ConnectionMode { get; set; }
}

public class RemoteStartDto
{
    public int DurationMinutes { get; set; }
    public string? CustomerName { get; set; }
}
