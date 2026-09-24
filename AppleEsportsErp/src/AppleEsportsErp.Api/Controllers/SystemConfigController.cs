using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Infrastructure.Data;
using System.Security.Claims;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/system-config")]
[Authorize(Policy = "Dashboard:settings")]
public class SystemConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public SystemConfigController(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllConfigs()
    {
        var configs = await _db.SystemConfigs.ToListAsync();
        var result = configs.Select(c => new
        {
            c.Id,
            c.ConfigKey,
            ConfigValue = JsonSerializer.Deserialize<object>(c.ConfigValue),
            c.Description,
            c.UpdatedAt
        });

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> SaveConfig([FromBody] SaveConfigDto dto)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.ConfigKey == dto.ConfigKey);
        
        if (config == null)
        {
            config = new SystemConfig
            {
                Id = Guid.NewGuid(),
                ConfigKey = dto.ConfigKey,
                ConfigValue = JsonSerializer.Serialize(dto.ConfigValue),
                Description = dto.Description,
                UpdatedBy = adminId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.SystemConfigs.Add(config);
        }
        else
        {
            config.ConfigValue = JsonSerializer.Serialize(dto.ConfigValue);
            config.Description = dto.Description ?? config.Description;
            config.UpdatedBy = adminId;
            config.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            config.Id,
            config.ConfigKey,
            ConfigValue = JsonSerializer.Deserialize<object>(config.ConfigValue),
            config.Description,
            config.UpdatedAt
        }));
    }

    /// <summary>
    /// Sends a real email right now and reports what happened, instead of the silent
    /// swallow-and-log-to-a-file that a real forgot-password or top-up email uses. This is
    /// how an admin finds out *why* mail isn't arriving without SSH-ing in to read a log.
    /// </summary>
    [HttpPost("test-email")]
    public async Task<IActionResult> TestEmail([FromBody] TestEmailDto dto)
    {
        var (success, message) = await _emailService.SendTestEmailAsync(dto.ToAddress);

        return Ok(success
            ? ApiResponse<object>.Ok(new { success = true, message })
            : ApiResponse<object>.Fail(message));
    }
}

public class TestEmailDto
{
    public string ToAddress { get; set; } = null!;
}

public class SaveConfigDto
{
    public string ConfigKey { get; set; } = null!;
    public object ConfigValue { get; set; } = null!;
    public string? Description { get; set; }
}
