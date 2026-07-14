using AppleEsportsErp.Application.DTOs.Common;
using AppleEsportsErp.Application.DTOs.Employees;

namespace AppleEsportsErp.Application.Interfaces;

public interface IEmployeeService
{
    Task<PaginatedResult<EmployeeDto>> GetEmployeesAsync(Guid branchId, string? search, int page, int pageSize);
    Task<EmployeeDto> GetEmployeeByIdAsync(Guid id);
    Task<EmployeeDto> CreateEmployeeAsync(Guid branchId, Guid? submittedBy, CreateEmployeeDto dto);
    Task<EmployeeDto> UpdateStatusAsync(Guid id, string status);
}
