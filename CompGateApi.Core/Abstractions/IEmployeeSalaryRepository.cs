// CompGateApi.Core.Abstractions/IEmployeeSalaryRepository.cs
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Models;

public interface IEmployeeSalaryRepository
{
    Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(int companyId, string? searchTerm, int page, int limit);
    Task<EmployeeDto> CreateEmployeeAsync(int companyId, EmployeeCreateDto dto);
    Task<EmployeeDto?> UpdateEmployeeAsync(int companyId, int id, EmployeeCreateDto dto);
    Task<bool> DeleteEmployeeAsync(int companyId, int id);
    Task<bool> BatchUpdateAsync(int companyId, List<EmployeeDto> updates);

    Task<PagedResult<SalaryCycleDto>> GetSalaryCyclesAsync(int companyId, int page, int limit);
    Task<SalaryCycleDto> CreateSalaryCycleAsync(int companyId, int createdByUserId, SalaryCycleCreateDto dto);
    Task<SalaryCycleDto?> PostSalaryCycleAsync(int companyId, int cycleId, int? postedByUserId);
}