// CompGateApi.Infrastructure/Repositories/EmployeeSalaryRepository.cs
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

public class EmployeeSalaryRepository : IEmployeeSalaryRepository
{
    private readonly CompGateApiDbContext _db;

    public EmployeeSalaryRepository(CompGateApiDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(int companyId, string? searchTerm, int page, int limit)
    {
        var query = _db.Employees.Where(e => e.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(e => e.Name.Contains(searchTerm) || e.Email.Contains(searchTerm));
        }

        var total = await query.CountAsync();
        var list = await query.Skip((page - 1) * limit).Take(limit).ToListAsync();

        return new PagedResult<EmployeeDto>
        {
            Page = page,
            Limit = limit,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)limit),
            Data = list.Select(e => new EmployeeDto
            {
                Id = e.Id,
                Name = e.Name,
                Email = e.Email,
                Phone = e.Phone,
                Salary = e.Salary,
                Date = e.Date,
                AccountNumber = e.AccountNumber,
                AccountType = e.AccountType,
                SendSalary = e.SendSalary,
                CanPost = e.CanPost
            }).ToList()
        };
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(int companyId, EmployeeCreateDto dto)
    {
        var e = new Employee
        {
            CompanyId = companyId,
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            Salary = dto.Salary,
            Date = dto.Date,
            AccountNumber = dto.AccountNumber,
            AccountType = dto.AccountType,
            SendSalary = dto.SendSalary,
            CanPost = dto.CanPost
        };

        _db.Employees.Add(e);
        await _db.SaveChangesAsync();

        return new EmployeeDto
        {
            Id = e.Id,
            Name = e.Name,
            Email = e.Email,
            Phone = e.Phone,
            Salary = e.Salary,
            Date = e.Date,
            AccountNumber = e.AccountNumber,
            AccountType = e.AccountType,
            SendSalary = e.SendSalary,
            CanPost = e.CanPost
        };
    }

    public async Task<EmployeeDto?> UpdateEmployeeAsync(int companyId, int id, EmployeeCreateDto dto)
    {
        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (e == null) return null;

        e.Name = dto.Name;
        e.Email = dto.Email;
        e.Phone = dto.Phone;
        e.Salary = dto.Salary;
        e.Date = dto.Date;
        e.AccountNumber = dto.AccountNumber;
        e.AccountType = dto.AccountType;
        e.SendSalary = dto.SendSalary;
        e.CanPost = dto.CanPost;

        await _db.SaveChangesAsync();

        return new EmployeeDto
        {
            Id = e.Id,
            Name = e.Name,
            Email = e.Email,
            Phone = e.Phone,
            Salary = e.Salary,
            Date = e.Date,
            AccountNumber = e.AccountNumber,
            AccountType = e.AccountType,
            SendSalary = e.SendSalary,
            CanPost = e.CanPost
        };
    }

    public async Task<bool> DeleteEmployeeAsync(int companyId, int id)
    {
        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (e == null) return false;

        _db.Employees.Remove(e);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> BatchUpdateAsync(int companyId, List<EmployeeDto> updates)
    {
        var ids = updates.Select(e => e.Id).ToList();
        var employees = await _db.Employees.Where(e => e.CompanyId == companyId && ids.Contains(e.Id)).ToListAsync();

        foreach (var e in employees)
        {
            var updated = updates.First(x => x.Id == e.Id);
            e.SendSalary = updated.SendSalary;
            e.CanPost = updated.CanPost;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<SalaryCycleDto>> GetSalaryCyclesAsync(int companyId, int page, int limit)
    {
        var query = _db.SalaryCycles
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.SalaryMonth)
            .Include(s => s.Entries).ThenInclude(e => e.Employee);

        var total = await query.CountAsync();
        var list = await query.Skip((page - 1) * limit).Take(limit).ToListAsync();

        return new PagedResult<SalaryCycleDto>
        {
            Page = page,
            Limit = limit,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)limit),
            Data = list.Select(s => new SalaryCycleDto
            {
                Id = s.Id,
                SalaryMonth = s.SalaryMonth,
                CreatedAt = s.CreatedAt,
                PostedAt = s.PostedAt,
                CreatedByUserId = s.CreatedByUserId,
                PostedByUserId = s.PostedByUserId,
                TotalAmount = s.TotalAmount,
                Entries = s.Entries.Select(e => new SalaryEntryDto
                {
                    Id = e.Id,
                    EmployeeId = e.EmployeeId,
                    EmployeeName = e.Employee.Name,
                    Amount = e.Amount,
                    IsTransferred = e.IsTransferred
                }).ToList()
            }).ToList()
        };
    }

    public async Task<SalaryCycleDto> CreateSalaryCycleAsync(int companyId, int createdByUserId, SalaryCycleCreateDto dto)
    {
        var employees = await _db.Employees
            .Where(e => e.CompanyId == companyId && e.SendSalary)
            .ToListAsync();

        var total = employees.Sum(e => e.Salary);

        var cycle = new SalaryCycle
        {
            CompanyId = companyId,
            SalaryMonth = dto.SalaryMonth,
            CreatedByUserId = createdByUserId,
            TotalAmount = total,
            Entries = employees.Select(e => new SalaryEntry
            {
                EmployeeId = e.Id,
                Amount = e.Salary
            }).ToList()
        };

        _db.SalaryCycles.Add(cycle);
        await _db.SaveChangesAsync();

        var result = await PostSalaryCycleAsync(companyId, cycle.Id, createdByUserId);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to post salary cycle.");
        }
        return result;
    }

    public async Task<SalaryCycleDto?> PostSalaryCycleAsync(int companyId, int cycleId, int? postedByUserId)
    {
        var cycle = await _db.SalaryCycles
            .Include(s => s.Entries)
            .ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(s => s.Id == cycleId && s.CompanyId == companyId);

        if (cycle == null) return null;

        cycle.PostedAt = DateTime.UtcNow;
        cycle.PostedByUserId = postedByUserId;

        foreach (var entry in cycle.Entries)
        {
            entry.IsTransferred = true;
        }

        await _db.SaveChangesAsync();

        return await GetSalaryCyclesAsync(companyId, 1, 50).ContinueWith(t => t.Result.Data.FirstOrDefault(x => x.Id == cycleId));
    }
}