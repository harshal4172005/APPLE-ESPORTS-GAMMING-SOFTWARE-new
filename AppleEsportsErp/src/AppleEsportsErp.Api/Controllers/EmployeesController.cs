using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Employees;
using AppleEsportsErp.Application.Interfaces;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Roles = "admin,super_admin")]
[BranchIsolation]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    private Guid GetBranchId()
    {
        var val = HttpContext.Items["BranchId"]?.ToString();
        return string.IsNullOrEmpty(val) ? Guid.Empty : Guid.Parse(val);
    }

    /// <summary>GET /api/employees — list all employees for this branch (Admin/SuperAdmin only)</summary>
    [HttpGet]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _employeeService.GetEmployeesAsync(GetBranchId(), search, page, pageSize);
        return Ok(ApiResponse<PaginatedResult<EmployeeDto>>.Ok(result));
    }

    /// <summary>GET /api/employees/{id} — get one employee by ID</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _employeeService.GetEmployeeByIdAsync(id);
        return Ok(ApiResponse<EmployeeDto>.Ok(result));
    }

    /// <summary>POST /api/employees — register a new employee via the joining form</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto)
    {
        var submittedBy = await this.GetOperatorIdAsync();
        var result = await _employeeService.CreateEmployeeAsync(GetBranchId(), submittedBy, dto);
        return Ok(ApiResponse<EmployeeDto>.Ok(result));
    }

    /// <summary>PATCH /api/employees/{id}/status — update employment status</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateEmployeeStatusDto dto)
    {
        var result = await _employeeService.UpdateStatusAsync(id, dto.Status);
        return Ok(ApiResponse<EmployeeDto>.Ok(result));
    }
}

public class UpdateEmployeeStatusDto
{
    public string Status { get; set; } = "Active";
}
