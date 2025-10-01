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
    Task<EmployeeDto?> GetEmployeeAsync(int companyId, int employeeId);

    Task<SalaryCycleDto?> GetSalaryCycleAsync(int companyId, int cycleId);

    Task<SalaryEntryDto?> GetSalaryEntryAsync(int companyId, int cycleId, int entryId);

    Task<PagedResult<SalaryCycleDto>> GetSalaryCyclesAsync(int companyId, int page, int limit);
    Task<SalaryCycleDto> CreateSalaryCycleAsync(int companyId, int createdByUserId, SalaryCycleCreateDto dto);
    // IEmployeeSalaryRepository.cs
    Task<SalaryCycleDto> PostSalaryCycleAsync(int companyId, int cycleId, int postedBy);

    Task<SalaryCycleDto?> SaveSalaryCycleAsync(int companyId, int cycleId, SalaryCycleSaveDto dto);

    Task<int> AdminGetSalaryCyclesCountAsync(
         string? companyCode,
         string? searchTerm,
         string? searchBy,
         DateTime? from,
         DateTime? to);

    Task<PagedResult<SalaryCycleAdminListItemDto>> AdminGetSalaryCyclesAsync(
        string? companyCode,
        string? searchTerm,
        string? searchBy,
        DateTime? from,
        DateTime? to,
        int page,
        int limit);

    Task<SalaryCycleAdminDetailDto?> AdminGetSalaryCycleAsync(int cycleId);

}