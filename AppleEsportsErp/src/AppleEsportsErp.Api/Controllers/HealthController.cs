using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Infrastructure.Data;
using System.Diagnostics;

namespace AppleEsportsErp.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Check()
    {
        var diagnostics = new Dictionary<string, object>
        {
            ["Timestamp"] = DateTime.UtcNow,
            ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            ["MachineName"] = Environment.MachineName,
            ["Version"] = "v2.0"
        };

        bool allHealthy = true;

        // 1. Check PostgreSQL (AWS RDS / Aurora)
        try
        {
            bool dbConnected = await _db.Database.CanConnectAsync();
            diagnostics["Database"] = dbConnected ? "Connected" : "Disconnected (Could not establish connection)";
            if (!dbConnected) allHealthy = false;
        }
        catch (Exception ex)
        {
            diagnostics["Database"] = $"Error: {ex.Message}";
            diagnostics["DatabaseErrorDetails"] = ex.ToString();
            allHealthy = false;
        }

        // 2. Check System Memory (AWS EC2 / ECS Task Health)
        try 
        {
            var process = Process.GetCurrentProcess();
            var memoryUsedMb = process.WorkingSet64 / (1024 * 1024);
            diagnostics["MemoryUsageMB"] = memoryUsedMb;
            
            // Set arbitrary threshold for warning, e.g., > 1024 MB
            if (memoryUsedMb > 1024) 
            {
                diagnostics["MemoryStatus"] = "High Usage - Warning";
            }
            else
            {
                diagnostics["MemoryStatus"] = "Normal";
            }
        }
        catch { }

        diagnostics["Status"] = allHealthy ? "Healthy" : "Degraded";
        
        return Ok(diagnostics);
    }
}

