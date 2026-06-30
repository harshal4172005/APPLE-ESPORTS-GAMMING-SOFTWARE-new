using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Settings;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Application.Constants;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/admins")]
[Authorize(Policy = "AdminOrSuperAdmin")]
public class AdminsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var admins = await _unitOfWork.Repository<User>()
            .Query()
            .Where(u => u.Role == Roles.Admin)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var dtos = admins.Select(a => new
        {
            a.Id,
            a.FullName,
            a.Email,
            Status = a.Status.ToString(),
            DashboardPermissions = a.DashboardPermissions ?? "{}",
            a.CreatedAt,
            Type = "User"
        }).ToList();

        var promotedOperators = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>()
            .Query()
            .Where(o => o.IsGlobalAdmin)
            .OrderBy(o => o.FullName)
            .ToListAsync();

        var opDtos = promotedOperators.Select(o => new
        {
            o.Id,
            o.FullName,
            Email = $"@{o.Username}",
            Status = o.Status.ToString(),
            DashboardPermissions = o.DashboardPermissions ?? "{}",
            o.CreatedAt,
            Type = "Operator"
        });

        dtos.AddRange(opDtos);

        return Ok(ApiResponse<object>.Ok(dtos.OrderBy(d => d.FullName)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAdminDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(ApiResponse<object>.Fail("Password is required."));

        var emailTaken = await _unitOfWork.Repository<User>().Query().AnyAsync(u => u.Email == dto.Email.Trim());
        if (emailTaken)
            return BadRequest(ApiResponse<object>.Fail("Email is already taken."));

        var admin = new User
        {
            Id = Guid.NewGuid(),
            FullName = dto.FullName,
            Email = dto.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = Roles.Admin,
            DashboardPermissions = dto.DashboardPermissions,
            Status = UserStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<User>().AddAsync(admin);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { admin.Id, admin.Email }));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAdminDto dto)
    {
        var admin = await _unitOfWork.Repository<User>().Query().FirstOrDefaultAsync(u => u.Id == id && u.Role == Roles.Admin);
        if (admin == null) return NotFound(ApiResponse<object>.Fail("Admin not found"));

        admin.FullName = dto.FullName;
        admin.Email = dto.Email.Trim();
        admin.DashboardPermissions = dto.DashboardPermissions;
        admin.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        _unitOfWork.Repository<User>().Update(admin);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { admin.Id, admin.Email }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var admin = await _unitOfWork.Repository<User>().Query().FirstOrDefaultAsync(u => u.Id == id && u.Role == Roles.Admin);
        if (admin == null) return NotFound(ApiResponse<object>.Fail("Admin not found"));

        admin.Status = UserStatus.Disabled;
        admin.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<User>().Update(admin);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { message = "Admin disabled successfully" }));
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var admin = await _unitOfWork.Repository<User>().Query().FirstOrDefaultAsync(u => u.Id == id && u.Role == Roles.Admin);
        if (admin == null) return NotFound(ApiResponse<object>.Fail("Admin not found"));

        admin.Status = UserStatus.Active;
        admin.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<User>().Update(admin);
        await _unitOfWork.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { message = "Admin activated successfully" }));
    }
}

public class CreateAdminDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DashboardPermissions { get; set; }
}

public class UpdateAdminDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? DashboardPermissions { get; set; }
}
