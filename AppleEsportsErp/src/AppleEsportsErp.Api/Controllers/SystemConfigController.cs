using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Application.DTOs.Common;
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

    public SystemConfigController(AppDbContext db)
    {
        _db = db;
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
}

public class SaveConfigDto
{
    public string ConfigKey { get; set; } = null!;
    public object ConfigValue { get; set; } = null!;
    public string? Description { get; set; }
}
