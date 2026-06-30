using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Application.DTOs.Settings;
using AppleEsportsErp.Application.DTOs.Common;

namespace AppleEsportsErp.Api.Controllers;

/// <summary>SOP §22: Immutable Audit Trail API</summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditLogsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    [Authorize(Policy = "Dashboard:settings")]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        // Super Admin can see all logs
        var query = _unitOfWork.Repository<AuditLog>().Query();
        
        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = logs.Select(a => new AuditLogDto
        {
            Id = a.Id,
            UserName = a.UserName,
            UserRole = a.UserRole,
            Action = a.Action,
            TargetType = a.TargetType,
            TargetId = a.TargetId,
            Details = a.Details,
            IpAddress = a.IpAddress,
            CreatedAt = a.CreatedAt
        });

        return Ok(ApiResponse<object>.Ok(dtos));
    }

    [HttpGet("branch")]
    [BranchIsolation]
    public async Task<IActionResult> GetBranchLogs([FromQuery] Guid? branchId = null, [FromQuery] int limit = 100, [FromQuery] int offset = 0)
    {
        var targetBranchId = branchId;
        if (targetBranchId == null && HttpContext.Items.TryGetValue("BranchId", out var itemVal) && itemVal != null)
        {
            targetBranchId = Guid.Parse(itemVal.ToString()!);
        }

        if (targetBranchId == null)
        {
            var assignedBranch = User.FindFirst("branchId")?.Value;
            if (!string.IsNullOrEmpty(assignedBranch))
            {
                targetBranchId = Guid.Parse(assignedBranch);
            }
        }

        if (targetBranchId == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Branch context required."));
        }
        
        var query = _unitOfWork.Repository<AuditLog>().Query()
            .Where(a => a.BranchId == targetBranchId.Value);
        
        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = logs.Select(a => new AuditLogDto
        {
            Id = a.Id,
            UserName = a.UserName,
            UserRole = a.UserRole,
            Action = a.Action,
            TargetType = a.TargetType,
            TargetId = a.TargetId,
            Details = a.Details,
            IpAddress = a.IpAddress,
            CreatedAt = a.CreatedAt
        });

        return Ok(ApiResponse<object>.Ok(dtos));
    }
}

