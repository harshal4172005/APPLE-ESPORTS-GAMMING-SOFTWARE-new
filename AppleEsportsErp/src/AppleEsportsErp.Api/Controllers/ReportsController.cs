using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Reports;
using AppleEsportsErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
[BranchIsolation]
public class ReportsController : ControllerBase
{
    private readonly IReportsService _reportsService;

    public ReportsController(IReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    private Guid GetBranchId() => Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);

    [HttpGet("cash-reconciliation")]
    public async Task<IActionResult> GetCashReconciliationReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var branchId = GetBranchId();
        var report = await _reportsService.GetCashReconciliationReportAsync(branchId, startDate, endDate);
        return Ok(ApiResponse<List<ReconciliationReportDto>>.Ok(report));
    }
}
