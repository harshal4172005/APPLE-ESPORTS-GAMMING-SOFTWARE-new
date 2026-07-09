using Microsoft.AspNetCore.Authorization;
using System;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AppleEsportsErp.Api.Extensions;
using AppleEsportsErp.Api.Filters;

namespace AppleEsportsErp.Api.Controllers;













/// <summary>SOP §13: Menu / Inventory — maps from inventory.routes.js</summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
[BranchIsolation]
public class InventoryController : ControllerBase
{
    private readonly AppleEsportsErp.Application.Interfaces.IUnitOfWork _unitOfWork;

    public InventoryController(AppleEsportsErp.Application.Interfaces.IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeAll = false, [FromQuery] Guid? branchId = null)
    {
        Console.WriteLine($"[DEBUG GetAll] HttpContext is null: {HttpContext == null}");
        if (HttpContext != null)
        {
            Console.WriteLine($"[DEBUG GetAll] HttpContext.Items is null: {HttpContext.Items == null}");
            Console.WriteLine($"[DEBUG GetAll] BranchId in Items: {HttpContext.Items["BranchId"]}");
        }
        var branchIdStr = HttpContext?.Items["BranchId"]?.ToString();
        var targetBranchId = branchId 
            ?? (string.IsNullOrEmpty(branchIdStr) ? (Guid?)null : Guid.Parse(branchIdStr));
            
        if (targetBranchId == null)
        {
            var firstBranch = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Query().FirstOrDefaultAsync();
            if (firstBranch == null)
            {
                return BadRequest(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("No branches found in the system"));
            }
            targetBranchId = firstBranch.Id;
        }
        
        var query = _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>()
            .Query()
            .Where(i => i.BranchId == targetBranchId);

        if (!includeAll)
        {
            query = query.Where(i => i.Status != AppleEsportsErp.Domain.Enums.FoodAvailability.Disabled);
        }

        var items = await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.ItemName)
            .ToListAsync();
            
        var dtos = items.Select(i => new {
            i.Id,
            i.ItemName,
            i.Category,
            i.Price,
            i.CurrentStock,
            i.SoldQty,
            i.MinStockLimit,
            Status = i.Status.ToString(),
            IsLowStock = i.CurrentStock <= i.MinStockLimit,
            i.ImageUrl,
            i.CreatedAt,
            i.UpdatedAt
        });

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(dtos));
    }

    [HttpPost]
    [Authorize(Policy = "Dashboard:menu_editor")]
    public async Task<IActionResult> Create([FromBody] CreateInventoryItemDto dto)
    {
        var targetBranchId = dto.BranchId ?? Guid.Parse(HttpContext.Items["BranchId"]!.ToString()!);
        
        var item = new AppleEsportsErp.Domain.Entities.InventoryItem
        {
            Id = Guid.NewGuid(),
            BranchId = targetBranchId,
            ItemName = dto.ItemName,
            Category = dto.Category,
            Price = dto.Price,
            CurrentStock = dto.CurrentStock,
            SoldQty = 0,
            MinStockLimit = dto.MinStockLimit,
            Status = dto.Status,
            ImageUrl = dto.ImageUrl,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>().AddAsync(item);
        
        // Log initial stock creation as "refill"
        var isOp = User.FindFirstValue(ClaimTypes.Role) == "operator";
        var log = new AppleEsportsErp.Domain.Entities.InventoryLog
        {
            Id = Guid.NewGuid(),
            InventoryId = item.Id,
            BranchId = targetBranchId,
            OperatorId = isOp ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) : (Guid?)null,
            Action = "refill",
            Quantity = dto.CurrentStock,
            OldValue = "0",
            NewValue = dto.CurrentStock.ToString(),
            Reason = "Initial menu item creation",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryLog>().AddAsync(log);

        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new {
            item.Id,
            item.ItemName,
            item.Category,
            item.Price,
            item.CurrentStock,
            item.SoldQty,
            item.MinStockLimit,
            Status = item.Status.ToString(),
            IsLowStock = item.CurrentStock <= item.MinStockLimit,
            item.ImageUrl,
            item.CreatedAt,
            item.UpdatedAt
        }));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Dashboard:menu_editor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInventoryItemDto dto)
    {
        var item = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>()
            .Query()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Inventory item not found"));

        var now = DateTimeOffset.UtcNow;
        var isOp = User.FindFirstValue(ClaimTypes.Role) == "operator";
        var logOperatorId = isOp ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) : (Guid?)null;

        // Audit changes if price/status/stock changed
        if (item.Price != dto.Price)
        {
            var log = new AppleEsportsErp.Domain.Entities.InventoryLog
            {
                Id = Guid.NewGuid(),
                InventoryId = item.Id,
                BranchId = item.BranchId,
                OperatorId = logOperatorId,
                Action = "price_change",
                OldValue = item.Price.ToString("F2"),
                NewValue = dto.Price.ToString("F2"),
                Reason = "Price updated via menu editor",
                CreatedAt = now
            };
            await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryLog>().AddAsync(log);
        }

        if (item.Status != dto.Status)
        {
            var log = new AppleEsportsErp.Domain.Entities.InventoryLog
            {
                Id = Guid.NewGuid(),
                InventoryId = item.Id,
                BranchId = item.BranchId,
                OperatorId = logOperatorId,
                Action = "status_change",
                OldValue = item.Status.ToString(),
                NewValue = dto.Status.ToString(),
                Reason = "Status updated via menu editor",
                CreatedAt = now
            };
            await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryLog>().AddAsync(log);
        }

        if (item.CurrentStock != dto.CurrentStock)
        {
            var log = new AppleEsportsErp.Domain.Entities.InventoryLog
            {
                Id = Guid.NewGuid(),
                InventoryId = item.Id,
                BranchId = item.BranchId,
                OperatorId = logOperatorId,
                Action = "refill",
                Quantity = dto.CurrentStock - item.CurrentStock,
                OldValue = item.CurrentStock.ToString(),
                NewValue = dto.CurrentStock.ToString(),
                Reason = "Stock count updated via menu editor",
                CreatedAt = now
            };
            await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryLog>().AddAsync(log);
        }

        item.ItemName = dto.ItemName;
        item.Category = dto.Category;
        item.Price = dto.Price;
        item.CurrentStock = dto.CurrentStock;
        item.MinStockLimit = dto.MinStockLimit;
        item.Status = dto.Status;
        item.ImageUrl = dto.ImageUrl;
        item.UpdatedAt = now;

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>().Update(item);
        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new {
            item.Id,
            item.ItemName,
            item.Category,
            item.Price,
            item.CurrentStock,
            item.SoldQty,
            item.MinStockLimit,
            Status = item.Status.ToString(),
            IsLowStock = item.CurrentStock <= item.MinStockLimit,
            item.ImageUrl,
            item.CreatedAt,
            item.UpdatedAt
        }));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Dashboard:menu_editor")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>()
            .Query()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Inventory item not found"));

        try
        {
            _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>().Remove(item);
            await _unitOfWork.SaveChangesAsync();
            return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = "Item deleted successfully" }));
        }
        catch (Exception)
        {
            // If deleting throws due to foreign keys, soft-delete by setting status to Disabled
            item.Status = AppleEsportsErp.Domain.Enums.FoodAvailability.Disabled;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>().Update(item);
            await _unitOfWork.SaveChangesAsync();
            return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = "Item cannot be permanently deleted due to existing orders. It has been deactivated instead." }));
        }
    }

    [HttpPost("{id}/reconcile")]
    [Authorize(Policy = "Dashboard:menu_editor")]
    public async Task<IActionResult> Reconcile(Guid id, [FromBody] ReconcileStockDto dto)
    {
        var item = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>()
            .Query()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Inventory item not found"));

        var oldStock = item.CurrentStock;
        var physicalCount = dto.PhysicalCount;
        var now = DateTimeOffset.UtcNow;
        var isOp = User.FindFirstValue(ClaimTypes.Role) == "operator";
        var logOperatorId = isOp ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) : (Guid?)null;

        item.CurrentStock = physicalCount;
        item.UpdatedAt = now;

        if (physicalCount == 0)
        {
            item.Status = AppleEsportsErp.Domain.Enums.FoodAvailability.OutOfStock;
        }
        else if (item.Status == AppleEsportsErp.Domain.Enums.FoodAvailability.OutOfStock && physicalCount > 0)
        {
            item.Status = AppleEsportsErp.Domain.Enums.FoodAvailability.Available;
        }

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>().Update(item);

        var log = new AppleEsportsErp.Domain.Entities.InventoryLog
        {
            Id = Guid.NewGuid(),
            InventoryId = item.Id,
            BranchId = item.BranchId,
            OperatorId = logOperatorId,
            Action = "discrepancy",
            Quantity = physicalCount - oldStock,
            OldValue = oldStock.ToString(),
            NewValue = physicalCount.ToString(),
            Reason = dto.Reason ?? "Physical inventory reconciliation count mismatch",
            CreatedAt = now
        };
        await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryLog>().AddAsync(log);

        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { item.Id, item.CurrentStock, Status = item.Status.ToString() }));
    }

    [HttpGet("discrepancies")]
    [Authorize(Policy = "Dashboard:reports")]
    public async Task<IActionResult> GetDiscrepancies([FromQuery] Guid? branchId = null)
    {
        Console.WriteLine($"[DEBUG GetDiscrepancies] HttpContext is null: {HttpContext == null}");
        if (HttpContext != null)
        {
            Console.WriteLine($"[DEBUG GetDiscrepancies] HttpContext.Items is null: {HttpContext.Items == null}");
            Console.WriteLine($"[DEBUG GetDiscrepancies] BranchId in Items: {HttpContext.Items["BranchId"]}");
        }
        var branchIdStr = HttpContext?.Items["BranchId"]?.ToString();
        var targetBranchId = branchId 
            ?? (string.IsNullOrEmpty(branchIdStr) ? (Guid?)null : Guid.Parse(branchIdStr));
            
        if (targetBranchId == null)
        {
            var firstBranch = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Query().FirstOrDefaultAsync();
            if (firstBranch == null)
            {
                return BadRequest(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("No branches found in the system"));
            }
            targetBranchId = firstBranch.Id;
        }

        var logs = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryLog>()
            .Query()
            .Include(l => l.InventoryItem)
            .Where(l => l.BranchId == targetBranchId && l.Action == "discrepancy")
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        var dtos = logs.Select(l => new {
            l.Id,
            l.InventoryId,
            ItemName = l.InventoryItem?.ItemName ?? "Unknown",
            l.OperatorId,
            l.Action,
            l.Quantity,
            l.OldValue,
            l.NewValue,
            l.Reason,
            l.CreatedAt
        });

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(dtos));
    }

    [HttpPatch("{id}/stock")]
    public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateStockRequest request)
    {
        var item = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>()
            .Query()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
            return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Inventory item not found"));

        item.CurrentStock = request.CurrentStock;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.InventoryItem>().Update(item);
        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { item.Id, item.CurrentStock }));
    }

    public record UpdateStockRequest(int CurrentStock);
}

public class CreateInventoryItemDto
{
    [Required]
    public string ItemName { get; set; } = null!;
    public string? Category { get; set; }
    [Required]
    [Range(0, 1000000)]
    public decimal Price { get; set; }
    [Required]
    [Range(0, 100000)]
    public int CurrentStock { get; set; }
    [Required]
    [Range(0, 100000)]
    public int MinStockLimit { get; set; } = 5;
    [Required]
    public AppleEsportsErp.Domain.Enums.FoodAvailability Status { get; set; } = AppleEsportsErp.Domain.Enums.FoodAvailability.Available;
    public string? ImageUrl { get; set; }
    public Guid? BranchId { get; set; }
}

public class UpdateInventoryItemDto
{
    [Required]
    public string ItemName { get; set; } = null!;
    public string? Category { get; set; }
    [Required]
    [Range(0, 1000000)]
    public decimal Price { get; set; }
    [Required]
    [Range(0, 100000)]
    public int CurrentStock { get; set; }
    [Required]
    [Range(0, 100000)]
    public int MinStockLimit { get; set; } = 5;
    [Required]
    public AppleEsportsErp.Domain.Enums.FoodAvailability Status { get; set; }
    public string? ImageUrl { get; set; }
}

public class ReconcileStockDto
{
    [Required]
    public int PhysicalCount { get; set; }
    public string? Reason { get; set; }
}






/// <summary>SOP §16: Branches — maps from branches.routes.js (Super Admin only)</summary>
[ApiController]
[Route("api/branches")]
[Authorize(Policy = "Dashboard:settings")]
public class BranchesController : ControllerBase
{
    private readonly AppleEsportsErp.Application.Interfaces.IUnitOfWork _unitOfWork;

    public BranchesController(AppleEsportsErp.Application.Interfaces.IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var branches = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>()
            .Query()
            .OrderBy(b => b.Name)
            .ToListAsync();

        var dtos = branches.Select(b => new AppleEsportsErp.Application.DTOs.Settings.BranchDto
        {
            Id = b.Id,
            Name = b.Name,
            Address = b.Address,
            OpeningTime = b.OpeningTime.ToString("HH:mm"),
            ClosingTime = b.ClosingTime.ToString("HH:mm"),
            Status = b.Status.ToString(),
            CreatedAt = b.CreatedAt
        });

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(dtos));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppleEsportsErp.Application.DTOs.Settings.CreateBranchDto dto)
    {
        var branch = new AppleEsportsErp.Domain.Entities.Branch
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Address = dto.Address,
            OpeningTime = TimeOnly.Parse(dto.OpeningTime),
            ClosingTime = TimeOnly.Parse(dto.ClosingTime),
            Status = AppleEsportsErp.Domain.Enums.BranchStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().AddAsync(branch);
        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { branch.Id, branch.Name }));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AppleEsportsErp.Application.DTOs.Settings.UpdateBranchDto dto)
    {
        var branch = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Query().FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Branch not found"));

        branch.Name = dto.Name;
        branch.Address = dto.Address;
        branch.OpeningTime = TimeOnly.Parse(dto.OpeningTime);
        branch.ClosingTime = TimeOnly.Parse(dto.ClosingTime);
        branch.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Update(branch);
        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { branch.Id, branch.Name }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var branch = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Query().FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Branch not found"));

        // Soft delete: toggle to Inactive
        branch.Status = AppleEsportsErp.Domain.Enums.BranchStatus.Inactive;
        branch.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Update(branch);
        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = "Branch deactivated successfully" }));
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var branch = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Query().FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Branch not found"));

        branch.Status = AppleEsportsErp.Domain.Enums.BranchStatus.Active;
        branch.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Update(branch);
        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = "Branch activated successfully" }));
    }

    [HttpDelete("{id}/permanent")]
    public async Task<IActionResult> DeletePermanent(Guid id)
    {
        var branch = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Query().FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Branch not found"));

        // Block deletion if the branch has any financial/session history — irreversible data loss
        var hasBills = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Bill>().Query().AnyAsync(b => b.BranchId == id);
        var hasSessions = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Session>().Query().AnyAsync(s => s.BranchId == id);
        var hasShifts = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Shift>().Query().AnyAsync(s => s.BranchId == id);

        if (hasBills || hasSessions || hasShifts)
        {
            return BadRequest(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail(
                "Cannot permanently delete this branch because it has transaction history (bills, sessions, or shifts). Deactivate the branch instead to preserve audit trails."));
        }

        // No financial history — safe to cascade delete in dependency order
        var pcs = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>().Query().Where(p => p.BranchId == id).ToListAsync();
        foreach (var pc in pcs)
            _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Pc>().Remove(pc);

        var operators = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Query().Where(o => o.BranchId == id).ToListAsync();
        foreach (var op in operators)
            _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Remove(op);

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Remove(branch);

        try
        {
            await _unitOfWork.SaveChangesAsync();
            return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = "Branch and its operators/rigs deleted permanently." }));
        }
        catch (DbUpdateException ex)
        {
            return BadRequest(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail(
                $"Deletion blocked by remaining linked records: {ex.InnerException?.Message ?? ex.Message}"));
        }
    }
}

/// <summary>SOP §5: Operators — maps from operators.routes.js</summary>
[ApiController]
[Route("api/operators")]
[Authorize(Policy = "Dashboard:settings")]
public class OperatorsController : ControllerBase
{
    private readonly AppleEsportsErp.Application.Interfaces.IUnitOfWork _unitOfWork;
    private readonly AppleEsportsErp.Application.Interfaces.IAuditService _auditService;
    private readonly AppleEsportsErp.Application.Interfaces.IEmailService _emailService;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<AppleEsportsErp.Api.Hubs.NotificationHub> _notificationHub;

    public OperatorsController(
        AppleEsportsErp.Application.Interfaces.IUnitOfWork unitOfWork, 
        AppleEsportsErp.Application.Interfaces.IAuditService auditService,
        AppleEsportsErp.Application.Interfaces.IEmailService emailService,
        Microsoft.AspNetCore.SignalR.IHubContext<AppleEsportsErp.Api.Hubs.NotificationHub> notificationHub)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _emailService = emailService;
        _notificationHub = notificationHub;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var operators = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>()
            .Query()
            .Where(o => !o.Username.StartsWith("system_admin_"))
            .Include(o => o.Branch)
            .OrderBy(o => o.FullName)
            .ToListAsync();

        var dtos = operators.Select(o => new AppleEsportsErp.Application.DTOs.Settings.OperatorDto
        {
            Id = o.Id,
            FullName = o.FullName,
            Username = o.Username,
            Email = o.Email,
            BranchId = o.BranchId,
            BranchName = o.Branch?.Name ?? "Unknown",
            Status = o.Status.ToString(),
            DashboardPermissions = o.DashboardPermissions ?? "{}",
            IsGlobalAdmin = o.IsGlobalAdmin,
            CreatedAt = o.CreatedAt
        });

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(dtos));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AppleEsportsErp.Application.DTOs.Settings.CreateOperatorDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Password is required to create an operator."));

        // Check username uniqueness within the branch
        var usernameTaken = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>()
            .Query().AnyAsync(o => o.Username == dto.Username.Trim() && o.BranchId == dto.BranchId);
        if (usernameTaken)
            return BadRequest(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail($"Username '{dto.Username}' is already taken in this branch."));

        var op = new AppleEsportsErp.Domain.Entities.Operator
        {
            Id = Guid.NewGuid(),
            FullName = dto.FullName,
            Username = dto.Username.Trim().ToLowerInvariant(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            BranchId = dto.BranchId,
            DashboardPermissions = dto.DashboardPermissions,
            Status = AppleEsportsErp.Domain.Enums.OperatorStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().AddAsync(op);
        await _unitOfWork.SaveChangesAsync();

        var branch = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Branch>().Query()
            .FirstOrDefaultAsync(b => b.Id == op.BranchId);
        var branchName = branch?.Name ?? "Unknown Branch";

        string emailBody = $@"
        <div style='background-color:#050505; color:#ffffff; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; padding:40px 20px; text-align:center;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #111111; border: 1px solid #333333; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.5);'>
                <div style='background: linear-gradient(135deg, #1a1a24 0%, #0d0d14 100%); padding: 30px 20px; border-bottom: 2px solid #dc2626;'>
                    <h1 style='margin: 0; font-size: 28px; letter-spacing: 2px; color: #ffffff; text-transform: uppercase;'>
                        <img src='https://appleesports.in/apple-touch-icon.png' alt='Logo' style='height: 40px; vertical-align: middle; margin-right: 15px;' /> APPLE ESPORTS
                    </h1>
                </div>
                <div style='padding: 40px 30px; text-align: left;'>
                    <h2 style='margin-top: 0; color: #a3a3a3; font-size: 24px; border-bottom: 2px solid #333333; padding-bottom: 15px;'>New Operator Registered</h2>
                    <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>A new operator account has been provisioned in the Apple Esports system.</p>
                    
                    <div style='background-color: #0a0a0a; border: 1px solid #222222; border-radius: 8px; padding: 20px; margin-top: 25px;'>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Name:</span> <strong style='color: #ffffff;'>{op.FullName}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Username:</span> <strong style='color: #ffffff;'>{op.Username}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Branch:</span> <strong style='color: #ffffff;'>{branchName}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Time:</span> <strong style='color: #ffffff;'>{op.CreatedAt.ToString("MMM dd, yyyy HH:mm")}</strong></p>
                    </div>
                </div>
                <div style='background-color: #080808; padding: 20px; border-top: 1px solid #222222; text-align: center;'>
                    <p style='margin: 0; color: #6b7280; font-size: 12px;'>This is an automated security notification from Apple Esports ERP.</p>
                    <p style='margin: 5px 0 0 0; color: #4b5563; font-size: 11px;'>© {DateTime.UtcNow.Year} Apple Esports. All rights reserved.</p>
                </div>
            </div>
        </div>";

        await SendNotificationAsync($"New Operator Joined: {op.FullName}", emailBody);

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { op.Id, op.Username }));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AppleEsportsErp.Application.DTOs.Settings.UpdateOperatorDto dto)
    {
        var op = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Query().FirstOrDefaultAsync(o => o.Id == id);
        if (op == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Operator not found"));

        op.FullName = dto.FullName;
        op.Username = dto.Username;
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            op.Email = dto.Email;
        }
        op.BranchId = dto.BranchId;
        op.DashboardPermissions = dto.DashboardPermissions;
        op.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            op.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        }

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Update(op);
        await _unitOfWork.SaveChangesAsync();

        await _notificationHub.Clients.Group($"user:{op.Id}").SendAsync("PermissionsUpdated");

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { op.Id, op.Username }));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var op = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Query().FirstOrDefaultAsync(o => o.Id == id);
        if (op == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Operator not found"));

        // Soft delete: toggle status to Disabled
        op.Status = AppleEsportsErp.Domain.Enums.OperatorStatus.Disabled;
        op.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Update(op);
        await _unitOfWork.SaveChangesAsync();

        string emailBody = $@"
        <div style='background-color:#050505; color:#ffffff; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; padding:40px 20px; text-align:center;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #111111; border: 1px solid #333333; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.5);'>
                <div style='background: linear-gradient(135deg, #1a1a24 0%, #0d0d14 100%); padding: 30px 20px; border-bottom: 2px solid #dc2626;'>
                    <h1 style='margin: 0; font-size: 28px; letter-spacing: 2px; color: #ffffff; text-transform: uppercase;'>
                        <img src='https://appleesports.in/apple-touch-icon.png' alt='Logo' style='height: 40px; vertical-align: middle; margin-right: 15px;' /> APPLE ESPORTS
                    </h1>
                </div>
                <div style='padding: 40px 30px; text-align: left;'>
                    <h2 style='margin-top: 0; color: #f59e0b; font-size: 24px; border-bottom: 2px solid #333333; padding-bottom: 15px;'>Operator Disabled</h2>
                    <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>An operator account has been disabled and its access has been revoked.</p>
                    
                    <div style='background-color: #0a0a0a; border: 1px solid #222222; border-radius: 8px; padding: 20px; margin-top: 25px;'>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Name:</span> <strong style='color: #ffffff;'>{op.FullName}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Username:</span> <strong style='color: #ffffff;'>{op.Username}</strong></p>
                        <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Time:</span> <strong style='color: #ffffff;'>{op.UpdatedAt.ToString("MMM dd, yyyy HH:mm")}</strong></p>
                    </div>
                </div>
                <div style='background-color: #080808; padding: 20px; border-top: 1px solid #222222; text-align: center;'>
                    <p style='margin: 0; color: #6b7280; font-size: 12px;'>This is an automated security notification from Apple Esports ERP.</p>
                    <p style='margin: 5px 0 0 0; color: #4b5563; font-size: 11px;'>© {DateTime.UtcNow.Year} Apple Esports. All rights reserved.</p>
                </div>
            </div>
        </div>";

        await SendNotificationAsync($"Operator Disabled: {op.FullName}", emailBody);

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = "Operator disabled successfully" }));
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var op = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Query().FirstOrDefaultAsync(o => o.Id == id);
        if (op == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Operator not found"));

        op.Status = AppleEsportsErp.Domain.Enums.OperatorStatus.Active;
        op.UpdatedAt = DateTimeOffset.UtcNow;
        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Update(op);
        await _unitOfWork.SaveChangesAsync();

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = "Operator activated successfully" }));
    }

    [HttpDelete("{id}/permanent")]
    public async Task<IActionResult> DeletePermanent(Guid id)
    {
        var op = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Query().FirstOrDefaultAsync(o => o.Id == id);
        if (op == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Operator not found"));

        try
        {
            _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Remove(op);
            await _unitOfWork.SaveChangesAsync();

            string emailBody = $@"
            <div style='background-color:#050505; color:#ffffff; font-family:""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; padding:40px 20px; text-align:center;'>
                <div style='max-width: 600px; margin: 0 auto; background-color: #111111; border: 1px solid #333333; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.5);'>
                    <div style='background: linear-gradient(135deg, #1a1a24 0%, #0d0d14 100%); padding: 30px 20px; border-bottom: 2px solid #dc2626;'>
                        <h1 style='margin: 0; font-size: 28px; letter-spacing: 2px; color: #ffffff; text-transform: uppercase;'>
                            <img src='https://appleesports.in/apple-touch-icon.png' alt='Logo' style='height: 40px; vertical-align: middle; margin-right: 15px;' /> APPLE ESPORTS
                        </h1>
                    </div>
                    <div style='padding: 40px 30px; text-align: left;'>
                        <h2 style='margin-top: 0; color: #ef4444; font-size: 24px; border-bottom: 2px solid #333333; padding-bottom: 15px;'>Operator Deleted</h2>
                        <p style='font-size: 16px; color: #d1d5db; line-height: 1.6;'>An operator account has been permanently deleted from the system.</p>
                        
                        <div style='background-color: #0a0a0a; border: 1px solid #222222; border-radius: 8px; padding: 20px; margin-top: 25px;'>
                            <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Name:</span> <strong style='color: #ffffff;'>{op.FullName}</strong></p>
                            <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Username:</span> <strong style='color: #ffffff;'>{op.Username}</strong></p>
                            <p style='margin: 10px 0;'><span style='color: #6b7280; display: inline-block; width: 100px;'>Time:</span> <strong style='color: #ffffff;'>{DateTimeOffset.UtcNow.ToString("MMM dd, yyyy HH:mm")}</strong></p>
                        </div>
                    </div>
                    <div style='background-color: #080808; padding: 20px; border-top: 1px solid #222222; text-align: center;'>
                        <p style='margin: 0; color: #6b7280; font-size: 12px;'>This is an automated security notification from Apple Esports ERP.</p>
                        <p style='margin: 5px 0 0 0; color: #4b5563; font-size: 11px;'>© {DateTime.UtcNow.Year} Apple Esports. All rights reserved.</p>
                    </div>
                </div>
            </div>";

            await SendNotificationAsync($"Operator Deleted: {op.FullName}", emailBody);

            return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = "Operator deleted permanently" }));
        }
        catch (DbUpdateException)
        {
            return BadRequest(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Cannot delete operator permanently because they have associated transactional history (shifts, sessions, bills, etc.). You can disable their account instead."));
        }
    }

    private async Task SendNotificationAsync(string subject, string body)
    {
        try 
        {
            System.IO.File.AppendAllText("email_log.txt", $"[OperatorsController] SendNotificationAsync hit! Subject: {subject}\n");
            var config = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.SystemConfig>().Query()
                .FirstOrDefaultAsync(c => c.ConfigKey == "global_system_rules");
            if (config != null && !string.IsNullOrEmpty(config.ConfigValue))
            {
                var doc = System.Text.Json.JsonDocument.Parse(config.ConfigValue);
                if (doc.RootElement.TryGetProperty("emailNotifications", out var emailNode))
                {
                    if (emailNode.TryGetProperty("receivers", out var receiversNode))
                    {
                        var receivers = receiversNode.GetString();
                        System.IO.File.AppendAllText("email_log.txt", $"[OperatorsController] Found receivers: '{receivers}'\n");
                        if (!string.IsNullOrWhiteSpace(receivers))
                        {
                            await _emailService.SendEmailAsync(receivers, subject, body);
                        }
                        else
                        {
                            System.IO.File.AppendAllText("email_log.txt", $"[OperatorsController] Receivers string was empty or whitespace.\n");
                        }
                    }
                    else
                    {
                        System.IO.File.AppendAllText("email_log.txt", $"[OperatorsController] 'receivers' property not found in JSON.\n");
                    }
                }
                else
                {
                    System.IO.File.AppendAllText("email_log.txt", $"[OperatorsController] 'emailNotifications' property not found in JSON.\n");
                }
            }
            else
            {
                System.IO.File.AppendAllText("email_log.txt", $"[OperatorsController] No config found in DB or it was empty.\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OperatorsController] Failed to send email notification: {ex.Message}");
            System.IO.File.AppendAllText("email_log.txt", $"[OperatorsController] Exception: {ex.ToString()}\n");
        }
    }

    [HttpPost("{id}/admin-role")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<IActionResult> ManageAdminRole(Guid id, [FromBody] AppleEsportsErp.Application.DTOs.Settings.ManageAdminRoleDto dto, [FromServices] AppleEsportsErp.Application.Interfaces.IAuthService authService)
    {
        var op = await _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Query().FirstOrDefaultAsync(o => o.Id == id);
        if (op == null) return NotFound(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Fail("Operator not found"));

        if (dto.IsGlobalAdmin)
        {
            // ── PROMOTE ──
            // Snapshot current permissions BEFORE adding admin perms, so we can restore them on demotion
            // Only snapshot if not already a global admin (to avoid overwriting original snapshot on re-promotion)
            if (!op.IsGlobalAdmin)
            {
                op.PreAdminDashboardPermissions = op.DashboardPermissions;
            }

            // Add admin-level permissions on top of existing permissions
            var currentPerms = new System.Collections.Generic.Dictionary<string, bool>();
            if (!string.IsNullOrEmpty(op.DashboardPermissions))
            {
                try { currentPerms = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, bool>>(op.DashboardPermissions) ?? new System.Collections.Generic.Dictionary<string, bool>(); }
                catch { }
            }
            currentPerms["settings"] = dto.CanAccessSettings;
            currentPerms["discount"] = dto.CanGiveDiscount;
            op.DashboardPermissions = System.Text.Json.JsonSerializer.Serialize(currentPerms);
        }
        else
        {
            // ── DEMOTE ──
            // Restore the permissions snapshot taken at promotion time
            if (!string.IsNullOrEmpty(op.PreAdminDashboardPermissions))
            {
                op.DashboardPermissions = op.PreAdminDashboardPermissions;
                op.PreAdminDashboardPermissions = null; // clear snapshot
            }
            else
            {
                // Fallback: no snapshot exists — just strip admin-only keys
                var currentPerms = new System.Collections.Generic.Dictionary<string, bool>();
                if (!string.IsNullOrEmpty(op.DashboardPermissions))
                {
                    try { currentPerms = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, bool>>(op.DashboardPermissions) ?? new System.Collections.Generic.Dictionary<string, bool>(); }
                    catch { }
                }
                currentPerms.Remove("settings");
                currentPerms.Remove("discount");
                op.DashboardPermissions = System.Text.Json.JsonSerializer.Serialize(currentPerms);
            }

            // Instantly revoke the demoted admin's session and force them to login again
            var adminIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(adminIdStr, out var adminId))
            {
                await authService.ForceLogoutAsync(adminId, op.Id);
            }
        }

        op.IsGlobalAdmin = dto.IsGlobalAdmin;
        op.UpdatedAt = DateTimeOffset.UtcNow;

        _unitOfWork.Repository<AppleEsportsErp.Domain.Entities.Operator>().Update(op);
        await _unitOfWork.SaveChangesAsync();

        var adminName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "System Admin";
        
        await _auditService.LogAsync(new AppleEsportsErp.Application.Interfaces.AuditEntry
        {
            UserId = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : Guid.Empty,
            UserRole = AppleEsportsErp.Application.Constants.Roles.SuperAdmin,
            UserName = adminName,
            Action = dto.IsGlobalAdmin ? "operator_promoted_to_admin" : "operator_demoted_from_admin",
            TargetType = "operator",
            TargetId = op.Id,
            BranchId = op.BranchId,
            Details = new { 
                operatorName = op.FullName, 
                canAccessSettings = dto.CanAccessSettings,
                canGiveDiscount = dto.CanGiveDiscount,
                permissionsRestored = !dto.IsGlobalAdmin && op.PreAdminDashboardPermissions == null
            }
        });

        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<object>.Ok(new { message = dto.IsGlobalAdmin ? "Operator promoted to Global Admin" : "Operator demoted — original permissions restored" }));
    }
}

/// <summary>SOP §18: Main Dashboard — maps from dashboard.routes.js</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
[BranchIsolation]
public class DashboardController : ControllerBase
{
    private readonly AppleEsportsErp.Application.Interfaces.IDashboardService _dashboardService;

    public DashboardController(AppleEsportsErp.Application.Interfaces.IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] Guid? branchId = null)
    {
        var targetBranchId = branchId;
        if (targetBranchId == null && HttpContext.Items.TryGetValue("BranchId", out var itemVal) && itemVal != null)
        {
            var parsedVal = itemVal.ToString();
            if (!string.IsNullOrEmpty(parsedVal) && Guid.TryParse(parsedVal, out var g))
            {
                targetBranchId = g;
            }
        }
        var result = await _dashboardService.GetSummaryAsync(targetBranchId);
        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<AppleEsportsErp.Application.DTOs.Dashboard.DashboardSummaryDto>.Ok(result));
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] Guid? branchId = null, [FromQuery] int limit = 20)
    {
        var targetBranchId = branchId;
        if (targetBranchId == null && HttpContext.Items.TryGetValue("BranchId", out var itemVal) && itemVal != null)
        {
            var parsedVal = itemVal.ToString();
            if (!string.IsNullOrEmpty(parsedVal) && Guid.TryParse(parsedVal, out var g))
            {
                targetBranchId = g;
            }
        }
        var result = await _dashboardService.GetRecentActivityAsync(targetBranchId, limit);
        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<IEnumerable<AppleEsportsErp.Application.DTOs.Dashboard.RecentActivityDto>>.Ok(result));
    }

    [HttpGet("branches-summary")]
    [Authorize(Policy = "OperatorOrAdmin")]
    public async Task<IActionResult> GetBranchSummaries()
    {
        var result = await _dashboardService.GetBranchSummariesAsync();
        return Ok(AppleEsportsErp.Application.DTOs.Common.ApiResponse<IEnumerable<AppleEsportsErp.Application.DTOs.Dashboard.BranchDashboardSummaryDto>>.Ok(result));
    }
}

