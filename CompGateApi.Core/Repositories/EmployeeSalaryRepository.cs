// CompGateApi.Infrastructure/Repositories/EmployeeSalaryRepository.cs
using System.Net.Http.Json;
using AutoMapper;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.Errors;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

public class EmployeeSalaryRepository : IEmployeeSalaryRepository
{
    private readonly CompGateApiDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IMapper _mapper;

    public EmployeeSalaryRepository(CompGateApiDbContext db, IHttpClientFactory http, IMapper mapper)
    {
        _db = db; _http = http; _mapper = mapper;
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
                DebitAccount = s.DebitAccount,
                Currency = s.Currency,
                CreatedAt = s.CreatedAt,
                PostedAt = s.PostedAt,
                CreatedByUserId = s.CreatedByUserId,
                PostedByUserId = s.PostedByUserId,
                TotalAmount = s.TotalAmount,
                Entries = s.Entries.Select(e => new SalaryEntryDto
                {
                    Id = e.Id,
                    EmployeeId = e.EmployeeId,
                    Name = e.Employee.Name,
                    Email = e.Employee.Email,
                    Phone = e.Employee.Phone,
                    Salary = e.Amount,
                    Date = e.Employee.Date,
                    AccountNumber = e.Employee.AccountNumber,
                    AccountType = e.Employee.AccountType,
                    SendSalary = e.Employee.SendSalary,
                    CanPost = e.Employee.CanPost,

                    /* payroll-specific */
                    IsTransferred = e.IsTransferred
                }).ToList()
            }).ToList()
        };
    }

    public async Task<SalaryCycleDto> CreateSalaryCycleAsync(int companyId, int createdByUserId, SalaryCycleCreateDto dto)
    {
        List<(Employee emp, decimal amount)> list;

        /* a) caller sent an explicit list ------------------------------- */
        if (dto.Entries is { Count: > 0 })
        {
            // fetch only those employees and keep the order from dto
            var empIds = dto.Entries.Select(e => e.EmployeeId).ToList();
            var empMap = await _db.Employees
                                 .Where(e => e.CompanyId == companyId &&
                                             empIds.Contains(e.Id))
                                 .ToDictionaryAsync(e => e.Id);

            list = dto.Entries
                     .Where(e => empMap.ContainsKey(e.EmployeeId))
                     .Select(e => (empMap[e.EmployeeId], e.Salary))
                     .ToList();
        }
        /* b) legacy behaviour ------------------------------------------ */
        else
        {
            var employees = await _db.Employees
                 .Where(e => e.CompanyId == companyId && e.SendSalary)
                 .ToListAsync();

            list = employees.Select(e => (e, e.Salary)).ToList();
        }

        var total = list.Sum(x => x.amount);
        var cycle = new SalaryCycle
        {
            CompanyId = companyId,
            SalaryMonth = dto.SalaryMonth,
            DebitAccount = dto.DebitAccount,
            Currency = dto.Currency,
            CreatedByUserId = createdByUserId,
            TotalAmount = total,
            Entries = list.Select(x => new SalaryEntry
            {
                EmployeeId = x.emp.Id,
                Amount = x.amount
            }).ToList()
        };

        _db.SalaryCycles.Add(cycle);
        await _db.SaveChangesAsync();

        return _mapper.Map<SalaryCycleDto>(cycle);
    }
    public async Task<SalaryCycleDto?> PostSalaryCycleAsync(int companyId, int cycleId, int postedBy)
    {
        // 1) load cycle
        var cycle = await _db.SalaryCycles
            .Include(c => c.Entries).ThenInclude(e => e.Employee)
            .Include(c => c.Company).ThenInclude(c => c.ServicePackage)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.CompanyId == companyId);

        if (cycle is null) return null;
        if (cycle.PostedAt != null) return null;

        // 2) package rule
        var pkgId = cycle.Company.ServicePackageId;
        var detail = await _db.ServicePackageDetails
            .Include(d => d.TransactionCategory)
            .FirstOrDefaultAsync(d => d.ServicePackageId == pkgId &&
                                      d.TransactionCategory.Name == "Salary Payment");
        if (detail is null || !detail.IsEnabledForPackage)
            throw new PayrollException("Salary payments are not enabled for your service package.");

        // 3) eligible entries (account type + 13 digits)
        var eligibleEntries = cycle.Entries
            .Where(e => e.Employee.AccountType.Equals("account", StringComparison.OrdinalIgnoreCase)
                     && e.Employee.AccountNumber?.Length == 13)
            .ToList();
        if (eligibleEntries.Count == 0)
            throw new PayrollException("No eligible employees (13-digit account numbers of type 'account') found.");

        // 4) (optional) validate per-entry limit + recompute total with commission if you use it
        // NOTE: If you still need commission, compute e.CommissionAmount here.

        const int SCALE = 1000; // 3 decimals
        const int PAD = 15;     // 000000000000000

        // Build 16-char IDs: yyyyMMddHHmm + "GT" + "00/01/.."
        var baseId = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var hid = baseId + "GT00";

        var list = new List<Dictionary<string, string>>();
        int i = 1;
        foreach (var e in eligibleEntries)
        {
            var net = e.Amount;
            var comm = e.CommissionAmount;          // 0 if you don't use commission
            var total = net + comm;

            var did = baseId + $"GT{i:00}"; // 16-char detail id
            i++;

            list.Add(new Dictionary<string, string>
            {
                ["YBCD06DID"] = did,
                ["YBCD06DACC"] = cycle.DebitAccount,
                ["YBCD06CACC"] = e.Employee.AccountNumber,
                ["YBCD06AMT"] = ((long)(net * SCALE)).ToString($"D{PAD}"),
                ["YBCD06CCY"] = cycle.Currency,
                ["YBCD06AMTC"] = ((long)(total * SCALE)).ToString($"D{PAD}"),
                ["YBCD06COMA"] = ((long)(comm * SCALE)).ToString($"D{PAD}"),
                ["YBCD06CNR3"] = "",
                ["YBCD06DNR2"] = "",
                ["YBCD06RESP"] = "",
                ["YBCD06RESPD"] = ""
            });
        }

        // Customer/CID: bank expects 6-digit customer number; derive from debit account
        var customerNumber = !string.IsNullOrEmpty(cycle.DebitAccount) && cycle.DebitAccount.Length >= 10
            ? cycle.DebitAccount.Substring(4, 6)
            : cycle.DebitAccount ?? "";

        var payload = new
        {
            Header = new
            {
                system = "MOBILE",
                referenceId = hid,              // MUST be 16 chars & match @HID
                userName = "TEDMOB",
                customerNumber = customerNumber,
                requestTime = DateTime.UtcNow.ToString("o"),
                language = "AR"
            },
            Details = new Dictionary<string, object>
            {
                ["@HID"] = hid,
                ["@APPLY"] = "Y",
                ["@APPLYALL"] = "N",
                ["GroupAccounts"] = list
            }
        };

        // 5) call Bank API
        var bankCli = _http.CreateClient("BankApi");
        var requestUri = new Uri(bankCli.BaseAddress!, "api/mobile/PostGroupTransfer");
        var bankRes = await bankCli.PostAsJsonAsync(requestUri, payload);

        var raw = await bankRes.Content.ReadAsStringAsync();
        if (!bankRes.IsSuccessStatusCode)
            throw new PayrollException($"Bank HTTP {(int)bankRes.StatusCode} at {requestUri}: {raw}");

        // Parse header to check success + collect per-item RESP
        // (we still need the raw for RESP mapping even when header is Success)
        BankResponseDto? bankDoc = null;
        try
        {
            bankDoc = System.Text.Json.JsonSerializer.Deserialize<BankResponseDto>(
                raw, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* fall through; we only need RESP values below */ }

        // Map successful items (RESP == "S") by Credit Account (CACC)
        var successAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var jdoc = System.Text.Json.JsonDocument.Parse(raw);
            if (jdoc.RootElement.TryGetProperty("Details", out var d1) &&
                d1.TryGetProperty("Details", out var d2) &&
                d2.TryGetProperty("GroupAccounts", out var arr) &&
                arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var resp = item.TryGetProperty("YBCD06RESP", out var rEl) ? rEl.GetString() : null;
                    var cacc = item.TryGetProperty("YBCD06CACC", out var cEl) ? cEl.GetString() : null;
                    if (string.Equals(resp, "S", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cacc))
                        successAccounts.Add(cacc.Trim());
                }
            }
        }
        catch
        {
            // if parsing fails, fall back to "all success" based on header
            if (string.Equals(bankDoc?.Header?.ReturnCode, "Success", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var e in eligibleEntries)
                    successAccounts.Add(e.Employee.AccountNumber ?? "");
            }
        }

        // If header indicates failure and we couldn't parse RESP -> surface message
        if (!string.Equals(bankDoc?.Header?.ReturnCode, "Success", StringComparison.OrdinalIgnoreCase) &&
            successAccounts.Count == 0)
        {
            throw new PayrollException($"Bank rejected payroll: {bankDoc?.Header?.ReturnMessage}");
        }

        // 6) update entries: only mark those with RESP == "S"
        var anySuccess = false;
        foreach (var e in eligibleEntries)
        {
            if (!string.IsNullOrEmpty(e.Employee.AccountNumber) &&
                successAccounts.Contains(e.Employee.AccountNumber))
            {
                e.IsTransferred = true;
                e.TransferredAt = DateTime.UtcNow;
                e.PostedByUserId = postedBy;
                anySuccess = true;
            }
        }

        // 7) set cycle-level PostedAt/PostedBy if ANY succeeded (your requested policy)
        if (anySuccess)
        {
            cycle.PostedAt = DateTime.UtcNow;
            cycle.PostedByUserId = postedBy;
        }

        await _db.SaveChangesAsync();
        return _mapper.Map<SalaryCycleDto>(cycle);
    }

    public async Task<EmployeeDto?> GetEmployeeAsync(int companyId, int employeeId)
    {
        var e = await _db.Employees
                         .AsNoTracking()
                         .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == employeeId);

        return e == null
            ? null
            : new EmployeeDto
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

    public async Task<SalaryCycleDto?> GetSalaryCycleAsync(int companyId, int cycleId)
    {
        var s = await _db.SalaryCycles
                         .Include(c => c.Entries).ThenInclude(en => en.Employee)
                         .AsNoTracking()
                         .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Id == cycleId);

        return s == null ? null : new SalaryCycleDto
        {
            Id = s.Id,
            SalaryMonth = s.SalaryMonth,
            DebitAccount = s.DebitAccount,
            Currency = s.Currency,
            CreatedAt = s.CreatedAt,
            PostedAt = s.PostedAt,
            CreatedByUserId = s.CreatedByUserId,
            PostedByUserId = s.PostedByUserId,
            TotalAmount = s.TotalAmount,
            Entries = s.Entries.Select(en => new SalaryEntryDto
            {
                Id = en.Id,
                EmployeeId = en.EmployeeId,
                /* identical employee fields */
                Name = en.Employee.Name,
                Email = en.Employee.Email,
                Phone = en.Employee.Phone,
                Salary = en.Amount,
                Date = en.Employee.Date,
                AccountNumber = en.Employee.AccountNumber,
                AccountType = en.Employee.AccountType,
                SendSalary = en.Employee.SendSalary,
                CanPost = en.Employee.CanPost,
                /* payroll-specific */
                IsTransferred = en.IsTransferred
            }).ToList()
        };
    }

    public async Task<SalaryEntryDto?> GetSalaryEntryAsync(int companyId, int cycleId, int entryId)
    {
        var en = await _db.SalaryEntries
                          .Include(e => e.Employee)
                          .Include(e => e.SalaryCycle)
                          .AsNoTracking()
                          .FirstOrDefaultAsync(e =>
                              e.Id == entryId &&
                              e.SalaryCycleId == cycleId &&
                              e.SalaryCycle.CompanyId == companyId);

        return en == null ? null : new SalaryEntryDto
        {
            Id = en.Id,
            EmployeeId = en.EmployeeId,
            /* identical employee fields */
            Name = en.Employee.Name,
            Email = en.Employee.Email,
            Phone = en.Employee.Phone,
            Salary = en.Amount,
            Date = en.Employee.Date,
            AccountNumber = en.Employee.AccountNumber,
            AccountType = en.Employee.AccountType,
            SendSalary = en.Employee.SendSalary,
            CanPost = en.Employee.CanPost,
            /* payroll-specific */
            IsTransferred = en.IsTransferred
        };
    }
    public async Task<SalaryCycleDto?> SaveSalaryCycleAsync(
       int companyId,
       int cycleId,
       SalaryCycleSaveDto dto)
    {

        var cycle = await _db.SalaryCycles
            .Include(c => c.Entries).ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.CompanyId == companyId);

        if (cycle == null || cycle.PostedAt != null)
            return null;

        if (dto.SalaryMonth is not null) cycle.SalaryMonth = dto.SalaryMonth.Value;
        if (dto.DebitAccount is not null) cycle.DebitAccount = dto.DebitAccount;
        if (dto.Currency is not null) cycle.Currency = dto.Currency;

        var incoming = (dto.Entries ?? Enumerable.Empty<SalaryEntryUpsertDto>())
                       .ToDictionary(e => e.EmployeeId, e => e);


        foreach (var existing in cycle.Entries.ToList())
        {
            if (incoming.TryGetValue(existing.EmployeeId, out var inc))
            {
                existing.Amount = inc.Salary;
                incoming.Remove(existing.EmployeeId);
            }
            else
            {
                _db.SalaryEntries.Remove(existing);
            }
        }

        foreach (var inc in incoming.Values)
        {
            var emp = await _db.Employees
                               .FirstOrDefaultAsync(e =>
                                   e.Id == inc.EmployeeId &&
                                   e.CompanyId == companyId);
            if (emp == null) continue;

            cycle.Entries.Add(new SalaryEntry
            {
                EmployeeId = emp.Id,
                Amount = inc.Salary
            });
        }

        cycle.TotalAmount = cycle.Entries.Sum(e => e.Amount);

        await _db.SaveChangesAsync();
        return _mapper.Map<SalaryCycleDto>(cycle);
    }


}