using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Employees;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Infrastructure.Services;

/// <summary>SOP §HR-01: Employee HR service — auto-generates employee numbers per branch</summary>
public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _db;

    public EmployeeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PaginatedResult<EmployeeDto>> GetEmployeesAsync(Guid branchId, string? search, int page, int pageSize)
    {
        var query = _db.Employees
            .Include(e => e.Branch)
            .Include(e => e.SubmittedByOperator)
            .Include(e => e.Operator)
            .Where(e => e.BranchId == branchId && !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.FullName.ToLower().Contains(search.ToLower()) ||
                                     (e.Phone != null && e.Phone.Contains(search)) ||
                                     e.EmployeeNumber.ToLower().Contains(search.ToLower()));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => MapToDto(e))
            .ToListAsync();

        return new PaginatedResult<EmployeeDto>(items, total, page, pageSize);
    }

    public async Task<EmployeeDto> GetEmployeeByIdAsync(Guid id)
    {
        var emp = await _db.Employees
            .Include(e => e.Branch)
            .Include(e => e.SubmittedByOperator)
            .Include(e => e.Operator)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted)
            ?? throw new NotFoundException("Employee not found");
        return MapToDto(emp);
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(Guid branchId, Guid? submittedBy, CreateEmployeeDto dto)
    {
        // Auto-generate employee number: APL-EMP-XXXX (global sequence)
        var count = await _db.Employees.CountAsync();
        var employeeNumber = $"APL-EMP-{(count + 1):D4}";

        var employee = new Employee
        {
            Id           = Guid.NewGuid(),
            BranchId     = branchId,
            EmployeeNumber = employeeNumber,
            FullName     = dto.FullName.Trim(),
            Gender       = dto.Gender,
            DateOfBirth  = dto.DateOfBirth,
            Nationality  = dto.Nationality ?? "Indian",
            MaritalStatus = dto.MaritalStatus,
            PermanentAddress = dto.PermanentAddress,
            CurrentAddress   = dto.CurrentAddress,
            Phone        = dto.Phone,
            Email        = dto.Email,
            EmergencyName         = dto.EmergencyName,
            EmergencyRelationship = dto.EmergencyRelationship,
            EmergencyPhone        = dto.EmergencyPhone,
            EmergencyEmail        = dto.EmergencyEmail,
            EmergencyAddress      = dto.EmergencyAddress,
            PositionTitle = dto.PositionTitle,
            Department    = dto.Department,
            Supervisor    = dto.Supervisor,
            StartDate     = dto.StartDate,
            BankName           = dto.BankName,
            AccountNumber      = dto.AccountNumber,
            AccountHolderName  = dto.AccountHolderName,
            BankBranch         = dto.BankBranch,
            RefName         = dto.RefName,
            RefRelationship = dto.RefRelationship,
            RefPhone        = dto.RefPhone,
            RefAddress      = dto.RefAddress,
            PhotoDataUrl  = dto.PhotoDataUrl,
            AadharDataUrl = dto.AadharDataUrl,
            Status      = "Active",
            SubmittedBy = submittedBy,
            CreatedAt   = DateTimeOffset.UtcNow,
            UpdatedAt   = DateTimeOffset.UtcNow
        };

        _db.Employees.Add(employee);

        // Optional System Account Creation (Operator/Admin)
        if (dto.CreateSystemAccount && !string.IsNullOrWhiteSpace(dto.SystemRole) && !string.IsNullOrWhiteSpace(dto.SystemUsername) && !string.IsNullOrWhiteSpace(dto.SystemPassword))
        {
            var isGlobalAdmin = dto.SystemRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            
            var basePermissions = new System.Collections.Generic.Dictionary<string, bool>
            {
                { "billing_counter", true },
                { "sessions", true },
                { "reservations", true },
                { "food_orders", true },
                { "cash_register", true },
                { "cash_desk", true },
                { "members", true },
                { "menu_editor", true },
                { "main_dashboard", true },
                { "pc_status", false },
                { "eod", false },
                { "settings", false },
                { "employee_forms", false },
                // On, for both operators and admins. Whoever is at the branch when an update
                // arrives has to be able to read what is in it.
                { "updates", true }
            };

            if (isGlobalAdmin)
            {
                basePermissions["settings"] = true;
                basePermissions["discount"] = true;
                basePermissions["employee_forms"] = true;
            }

            var op = new Operator
            {
                Id = Guid.NewGuid(),
                FullName = dto.FullName.Trim(),
                Username = dto.SystemUsername.Trim().ToLowerInvariant(),
                Email = dto.Email?.Trim().ToLowerInvariant() ?? $"{dto.SystemUsername.Trim().ToLowerInvariant()}@appleesports.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.SystemPassword),
                BranchId = branchId,
                DashboardPermissions = System.Text.Json.JsonSerializer.Serialize(basePermissions),
                Status = OperatorStatus.Active,
                IsGlobalAdmin = isGlobalAdmin,
                AccessPin = isGlobalAdmin ? dto.SystemPin : null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _db.Operators.Add(op);

            // Set here, not read back after SaveChanges — this is what lets DeleteEmployeeAsync
            // later find the right account to suspend without guessing from a name or phone
            // number that might not even be unique.
            employee.OperatorId = op.Id;
        }

        await _db.SaveChangesAsync();

        return await GetEmployeeByIdAsync(employee.Id);
    }

    public async Task<EmployeeDto> UpdateStatusAsync(Guid id, string status)
    {
        var emp = await _db.Employees.FindAsync(id)
            ?? throw new NotFoundException("Employee not found");
        emp.Status = status;
        emp.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return await GetEmployeeByIdAsync(id);
    }

    public async Task<(bool OperatorSuspended, string? OperatorName)> DeleteEmployeeAsync(Guid id)
    {
        var emp = await _db.Employees.FindAsync(id)
            ?? throw new NotFoundException("Employee not found");

        emp.IsDeleted = true;
        emp.UpdatedAt = DateTimeOffset.UtcNow;

        var suspended = false;
        string? operatorName = null;

        if (emp.OperatorId is { } operatorId)
        {
            var op = await _db.Operators.FindAsync(operatorId);

            // Never overwrite Disabled - that is the deliberate, permanent admin action from
            // Settings, and this is only ever the automatic side effect of removing the HR
            // record that created the account.
            if (op is not null && op.Status is not (OperatorStatus.Suspended or OperatorStatus.Disabled))
            {
                op.Status = OperatorStatus.Suspended;
                op.UpdatedAt = DateTimeOffset.UtcNow;
                suspended = true;
                operatorName = op.FullName;
            }
        }

        await _db.SaveChangesAsync();
        return (suspended, operatorName);
    }

    private static EmployeeDto MapToDto(Employee e) => new()
    {
        Id             = e.Id,
        BranchId       = e.BranchId,
        BranchName     = e.Branch?.Name ?? "",
        EmployeeNumber = e.EmployeeNumber,
        FullName       = e.FullName,
        Gender         = e.Gender,
        DateOfBirth    = e.DateOfBirth,
        Nationality    = e.Nationality,
        MaritalStatus  = e.MaritalStatus,
        PermanentAddress = e.PermanentAddress,
        CurrentAddress   = e.CurrentAddress,
        Phone  = e.Phone,
        Email  = e.Email,
        EmergencyName         = e.EmergencyName,
        EmergencyRelationship = e.EmergencyRelationship,
        EmergencyPhone        = e.EmergencyPhone,
        EmergencyEmail        = e.EmergencyEmail,
        EmergencyAddress      = e.EmergencyAddress,
        PositionTitle = e.PositionTitle,
        Department    = e.Department,
        Supervisor    = e.Supervisor,
        StartDate     = e.StartDate,
        BankName          = e.BankName,
        AccountNumber     = e.AccountNumber,
        AccountHolderName = e.AccountHolderName,
        BankBranch        = e.BankBranch,
        RefName         = e.RefName,
        RefRelationship = e.RefRelationship,
        RefPhone        = e.RefPhone,
        RefAddress      = e.RefAddress,
        PhotoDataUrl  = e.PhotoDataUrl,
        AadharDataUrl = e.AadharDataUrl,
        Status            = DisplayStatus(e),
        SubmittedByName   = e.SubmittedByOperator?.FullName,
        CreatedAt         = e.CreatedAt,
        OperatorId        = e.OperatorId,
    };

    /// <summary>
    /// Employee.Status is set once, at creation, and nothing has ever updated it since - so an
    /// operator suspended or disabled from Settings (not by removing this HR record) left this
    /// screen showing "Active" forever no matter what actually happened to the account. The
    /// operator record is the one place that state genuinely changes, so it is read fresh here
    /// instead of trusting the employee's own frozen copy.
    ///
    /// LoggedOut deliberately still reads as Active: it flips on every ordinary shift-end and
    /// every heartbeat that finds nobody on duty, and showing "inactive" for someone simply not
    /// clocked in right now would make this column read wrong the moment anyone goes home.
    /// Only Suspended and Disabled are genuine "this person is not working here" facts.
    /// </summary>
    private static string DisplayStatus(Employee e)
    {
        if (e.Operator is { } op)
        {
            return op.Status switch
            {
                OperatorStatus.Suspended => "Suspended",
                OperatorStatus.Disabled => "Disabled",
                _ => "Active",
            };
        }

        return e.Status;
    }
}
