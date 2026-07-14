using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Employees;
using AppleEsportsErp.Application.Exceptions;
using AppleEsportsErp.Application.Interfaces;
using AppleEsportsErp.Domain.Entities;
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
            .Where(e => e.BranchId == branchId);

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
            .FirstOrDefaultAsync(e => e.Id == id)
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
            Status      = "Active",
            SubmittedBy = submittedBy,
            CreatedAt   = DateTimeOffset.UtcNow,
            UpdatedAt   = DateTimeOffset.UtcNow
        };

        _db.Employees.Add(employee);
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
        Status            = e.Status,
        SubmittedByName   = e.SubmittedByOperator?.FullName,
        CreatedAt         = e.CreatedAt,
    };
}
