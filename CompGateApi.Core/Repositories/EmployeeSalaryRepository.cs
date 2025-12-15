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


    private static string NormalizeAcc13(string? acc)
    {
        if (string.IsNullOrWhiteSpace(acc)) return acc ?? "";
        return acc.Trim().PadLeft(13, '0');
    }

    // fee GL builder: {BRANCH}{932702}{CCY3 from sender}
    private static string BuildCommissionGlFromSender(string senderAcc13)
    {
        var acc = NormalizeAcc13(senderAcc13);
        if (acc.Length != 13) throw new Exception("Sender/debit account must be 13 digits.");
        var branch = acc.Substring(0, 4);
        var ccy3 = acc.Substring(10, 3); // last 3
        return $"{branch}932702{ccy3}";
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
        // 1) Load cycle
        var cycle = await _db.SalaryCycles
            .Include(c => c.Entries).ThenInclude(e => e.Employee)
            .Include(c => c.Company).ThenInclude(c => c.ServicePackage)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.CompanyId == companyId);

        if (cycle is null) return null;
        if (cycle.PostedAt != null) return null;

        // 2) Package rule (Salary Payment enabled)
        var pkgId = cycle.Company.ServicePackageId;
        var detail = await _db.ServicePackageDetails
            .Include(d => d.TransactionCategory)
            .FirstOrDefaultAsync(d => d.ServicePackageId == pkgId &&
                                      d.TransactionCategory.Name == "Salary Payment");
        if (detail is null || !detail.IsEnabledForPackage)
            throw new PayrollException("Salary payments are not enabled for your service package.");

        // 3) Eligible entries (account + 13 digits)
        var eligibleEntries = cycle.Entries
            .Where(e => e.Employee != null
                        && e.Employee.AccountType.Equals("account", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(e.Employee.AccountNumber)
                        && e.Employee.AccountNumber!.Length == 13)
            .ToList();
        if (eligibleEntries.Count == 0)
            throw new PayrollException("No eligible employees (13-digit account numbers of type 'account') found.");

        // 4) Load fixed fee pricing (TrxCatId = 17, Unit = 1)
        const int TRXCAT_SALARY_FIXED_FEE = 17;
        var feePricing = await _db.Pricings.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TrxCatId == TRXCAT_SALARY_FIXED_FEE && p.Unit == 1);
        if (feePricing == null)
            throw new PayrollException("Pricing for salary fixed fee (TrxCatId=17, Unit=1) is not configured.");

        var fixedFee = feePricing.Price;
        if (fixedFee is null || fixedFee <= 0m)
            throw new PayrollException("Invalid pricing: fixed fee must be greater than zero.");

        // Fee GL (prefer configured GL1, fallback to derived from sender)
        // Fee narration (prefer NR2, fallback default)
        var debitAcc13 = NormalizeAcc13(cycle.DebitAccount);
        if (debitAcc13.Length != 13)
            throw new PayrollException("Debit account must be 13 digits.");

        var feeGl = !string.IsNullOrWhiteSpace(feePricing.GL1)
            ? NormalizeAcc13(feePricing.GL1)
            : BuildCommissionGlFromSender(debitAcc13);
        if (feeGl.Length != 13)
            throw new PayrollException("Configured fee GL (GL1) must be a 13-digit account.");

        var feeNarration = string.IsNullOrWhiteSpace(feePricing.NR2)
            ? "Salary receiver-paid fixed fee"
            : feePricing.NR2;

        // 5) Build EMPLOYEE-ONLY payload (no fee lines here)
        // LYD uses 3 decimals, otherwise 2
        int SCALE = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 1000 : 100;
        const int PAD = 15;

        var baseId = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var hidEmp = baseId + "GT00";           // 16 chars
        cycle.BankReference = hidEmp;           // save immediately
        await _db.SaveChangesAsync();

        var groupAccountsEmp = new List<Dictionary<string, string>>();
        int i = 1;

        foreach (var e in eligibleEntries)
        {
            var amount = e.Amount;
            var fee = fixedFee.Value;
            if (amount < fee)
                throw new PayrollException($"Salary {amount:0.###} LYD is less than the fixed fee {fee:0.###} LYD (employee #{e.EmployeeId}).");

            // Gross credit to employee; fee will be handled in a separate per-employee debit
            var employeeCredit = amount;  // GROSS to employee
            var didEmp = baseId + $"GT{i:00}"; i++;

            groupAccountsEmp.Add(new Dictionary<string, string>
            {
                ["YBCD06DID"] = didEmp,
                ["YBCD06DACC"] = debitAcc13,
                ["YBCD06CACC"] = e.Employee!.AccountNumber!,
                ["YBCD06AMT"] = ((long)(Math.Round(employeeCredit, SCALE == 1000 ? 3 : 2) * SCALE)).ToString($"D{PAD}"),
                ["YBCD06CCY"] = cycle.Currency,
                ["YBCD06AMTC"] = ((long)(Math.Round(employeeCredit, SCALE == 1000 ? 3 : 2) * SCALE)).ToString($"D{PAD}"),
                ["YBCD06COMA"] = "000000000000000",
                ["YBCD06CNR3"] = "",
                ["YBCD06DNR2"] = "",
                ["YBCD06RESP"] = "",
                ["YBCD06RESPD"] = ""
            });
        }

        var customerNumber = debitAcc13.Substring(4, 6);
        var payloadEmp = new
        {
            Header = new
            {
                system = "MOBILE",
                referenceId = hidEmp,
                userName = "TEDMOB",
                customerNumber = customerNumber,
                requestTime = DateTime.UtcNow.ToString("o"),
                language = "AR"
            },
            Details = new Dictionary<string, object>
            {
                ["@HID"] = hidEmp,
                ["@APPLY"] = "Y",
                ["@APPLYALL"] = "N",
                ["GroupAccounts"] = groupAccountsEmp
            }
        };

        // 6) Call bank for EMPLOYEES transfer
        var bankCli = _http.CreateClient("BankApi");
        var requestUri = new Uri(bankCli.BaseAddress!, "api/mobile/PostGroupTransfer");
        var bankResEmp = await bankCli.PostAsJsonAsync(requestUri, payloadEmp);
        var rawEmp = await bankResEmp.Content.ReadAsStringAsync();

        cycle.BankResponseRaw = rawEmp;         // keep raw (success or fail)
        await _db.SaveChangesAsync();

        if (!bankResEmp.IsSuccessStatusCode)
            throw new PayrollException($"Bank HTTP {(int)bankResEmp.StatusCode} at {requestUri}: {rawEmp}");

        // 7) Parse success accounts for employees
        var successAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var jdoc = System.Text.Json.JsonDocument.Parse(rawEmp);
            if (jdoc.RootElement.TryGetProperty("Details", out var d1) &&
                d1.TryGetProperty("Details", out var d2) &&
                d2.TryGetProperty("GroupAccounts", out var arr) &&
                arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var resp = item.TryGetProperty("YBCD06RESP", out var rEl) ? rEl.GetString() : null;
                    var cacc = item.TryGetProperty("YBCD06CACC", out var cEl) ? cEl.GetString() : null;
                    if (string.Equals(resp, "S", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(cacc))
                    {
                        successAccounts.Add(cacc.Trim());
                    }
                }
            }
        }
        catch
        {
            // Fallback: treat whole bulk as success if top-level says Success
            try
            {
                var bankDoc = System.Text.Json.JsonSerializer.Deserialize<BankResponseDto>(
                    rawEmp, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (string.Equals(bankDoc?.Header?.ReturnCode, "Success", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var e in eligibleEntries)
                        successAccounts.Add(e.Employee!.AccountNumber!);
                }
            }
            catch { /* ignore */ }
        }

        // Mark successes & compute gross/fee totals for successful only
        var anySuccess = false;
        decimal successGrossTotal = 0m;
        int successCount = 0;

        foreach (var e in eligibleEntries)
        {
            if (successAccounts.Contains(e.Employee!.AccountNumber!))
            {
                e.IsTransferred = true;
                e.TransferredAt = DateTime.UtcNow;
                e.PostedByUserId = postedBy;
                successGrossTotal += e.Amount; // GROSS salary
                successCount++;
                anySuccess = true;
            }
        }

        if (!anySuccess)
            throw new PayrollException("Bank rejected all employee transfers.");

        // Update cycle totals to ONLY successful gross
        cycle.TotalAmount = successGrossTotal;

        // 8) Per-employee fee settlement: debit employee, credit fee GL (separate transactions)
        if (successCount > 0)
        {
            var feeLogs = new List<object>();
            var baseIdFee = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            int j = 1;

            foreach (var e in eligibleEntries)
            {
                if (!successAccounts.Contains(e.Employee!.AccountNumber!))
                    continue;

                var empAcc13 = NormalizeAcc13(e.Employee!.AccountNumber!);
                var employeeCustomer = empAcc13.Substring(4, 6);

                var hidFee = baseIdFee + $"GF{j:00}"; // unique reference per fee debit
                var didFee = baseIdFee + $"GF{j:00}"; j++;

                var groupAccountsFee = new List<Dictionary<string, string>>
                {
                    new()
                    {
                        ["YBCD06DID"] = didFee,
                        ["YBCD06DACC"] = empAcc13,
                        ["YBCD06CACC"] = feeGl,
                        ["YBCD06AMT"]  = ((long)(Math.Round(fixedFee.Value, SCALE == 1000 ? 3 : 2) * SCALE)).ToString($"D{PAD}"),
                        ["YBCD06CCY"]  = cycle.Currency,
                        ["YBCD06AMTC"] = ((long)(Math.Round(fixedFee.Value, SCALE == 1000 ? 3 : 2) * SCALE)).ToString($"D{PAD}"),
                        ["YBCD06COMA"] = "000000000000000",
                        ["YBCD06CNR3"] = "",
                        ["YBCD06DNR2"] = feeNarration,
                        ["YBCD06RESP"] = "",
                        ["YBCD06RESPD"] = ""
                    }
                };

                var payloadFee = new
                {
                    Header = new
                    {
                        system = "MOBILE",
                        referenceId = hidFee,
                        userName = "TEDMOB",
                        customerNumber = employeeCustomer,
                        requestTime = DateTime.UtcNow.ToString("o"),
                        language = "AR"
                    },
                    Details = new Dictionary<string, object>
                    {
                        ["@HID"] = hidFee,
                        ["@APPLY"] = "Y",
                        ["@APPLYALL"] = "N",
                        ["GroupAccounts"] = groupAccountsFee
                    }
                };

                var bankResFee = await bankCli.PostAsJsonAsync(requestUri, payloadFee);
                var rawFee = await bankResFee.Content.ReadAsStringAsync();

                feeLogs.Add(new { account = empAcc13, reference = hidFee, response = rawFee, http = (int)bankResFee.StatusCode });

                // Track fee amount on entry for auditing
                e.CommissionAmount = fixedFee.Value;

                // Do not fail the cycle if a fee debit fails; behavior mirrors previous policy
            }

            cycle.BankFeeReference = "MULTI";
            cycle.BankFeeResponseRaw = System.Text.Json.JsonSerializer.Serialize(
                feeLogs,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            await _db.SaveChangesAsync();
        }

        // 9) Mark cycle posted
        cycle.PostedAt = DateTime.UtcNow;
        cycle.PostedByUserId = postedBy;

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

    #region ADMIN-SALARYCYCLES

    public async Task<int> AdminGetSalaryCyclesCountAsync(
        string? companyCode,
        string? searchTerm,
        string? searchBy,
        DateTime? from,
        DateTime? to)
    {
        var q = _db.SalaryCycles
            .Include(s => s.Company)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(companyCode))
            q = q.Where(s => s.Company.Code.Contains(companyCode));

        if (from.HasValue)
            q = q.Where(s => s.SalaryMonth >= from.Value.Date);

        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            q = q.Where(s => s.SalaryMonth < end);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            switch ((searchBy ?? "").ToLower())
            {
                case "debitaccount":
                    q = q.Where(s => s.DebitAccount.Contains(searchTerm));
                    break;
                case "currency":
                    q = q.Where(s => s.Currency.Contains(searchTerm));
                    break;
                case "bankreference":
                    q = q.Where(s => s.BankReference != null && s.BankReference.Contains(searchTerm));
                    break;
                case "companyname":
                    q = q.Where(s => s.Company.Name.Contains(searchTerm));
                    break;
                case "companycode":
                    q = q.Where(s => s.Company.Code.Contains(searchTerm));
                    break;
                default:
                    q = q.Where(s =>
                        s.Company.Code.Contains(searchTerm) ||
                        s.Company.Name.Contains(searchTerm) ||
                        s.DebitAccount.Contains(searchTerm) ||
                        s.Currency.Contains(searchTerm) ||
                        (s.BankReference != null && s.BankReference.Contains(searchTerm)));
                    break;
            }
        }

        return await q.CountAsync();
    }

    public async Task<PagedResult<SalaryCycleAdminListItemDto>> AdminGetSalaryCyclesAsync(
        string? companyCode,
        string? searchTerm,
        string? searchBy,
        DateTime? from,
        DateTime? to,
        int page,
        int limit)
    {
        var q = _db.SalaryCycles
            .Include(s => s.Company)
            .AsNoTracking()
            .AsQueryable();


        if (!string.IsNullOrWhiteSpace(companyCode))
            q = q.Where(s => s.Company.Code.Contains(companyCode));

        if (from.HasValue)
            q = q.Where(s => s.SalaryMonth >= from.Value.Date);

        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            q = q.Where(s => s.SalaryMonth < end);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            switch ((searchBy ?? "").ToLower())
            {
                case "debitaccount":
                    q = q.Where(s => s.DebitAccount.Contains(searchTerm));
                    break;
                case "currency":
                    q = q.Where(s => s.Currency.Contains(searchTerm));
                    break;
                case "bankreference":
                    q = q.Where(s => s.BankReference != null && s.BankReference.Contains(searchTerm));
                    break;
                case "companyname":
                    q = q.Where(s => s.Company.Name.Contains(searchTerm));
                    break;
                case "companycode":
                    q = q.Where(s => s.Company.Code.Contains(searchTerm));
                    break;
                default:
                    q = q.Where(s =>
                        s.Company.Code.Contains(searchTerm) ||
                        s.Company.Name.Contains(searchTerm) ||
                        s.DebitAccount.Contains(searchTerm) ||
                        s.Currency.Contains(searchTerm) ||
                        (s.BankReference != null && s.BankReference.Contains(searchTerm)));
                    break;
            }
        }

        var total = await q.CountAsync();

        var data = await q
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(s => new SalaryCycleAdminListItemDto
            {
                Id = s.Id,
                CompanyId = s.CompanyId,
                CompanyCode = s.Company.Code,
                CompanyName = s.Company.Name,
                SalaryMonth = s.SalaryMonth,
                DebitAccount = s.DebitAccount,
                Currency = s.Currency,
                CreatedAt = s.CreatedAt,
                PostedAt = s.PostedAt,
                CreatedByUserId = s.CreatedByUserId,
                PostedByUserId = s.PostedByUserId,
                TotalAmount = s.TotalAmount,
                BankReference = s.BankReference
            })
            .ToListAsync();

        return new PagedResult<SalaryCycleAdminListItemDto>
        {
            Data = data,
            Page = page,
            Limit = limit,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)limit)
        };
    }

    public async Task<SalaryCycleAdminDetailDto?> AdminGetSalaryCycleAsync(int cycleId)
    {
        var s = await _db.SalaryCycles
            .Include(x => x.Company)
            .Include(x => x.Entries).ThenInclude(e => e.Employee)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cycleId);

        if (s == null) return null;

        return new SalaryCycleAdminDetailDto
        {
            Id = s.Id,
            CompanyId = s.CompanyId,
            CompanyCode = s.Company.Code,
            CompanyName = s.Company.Name,
            SalaryMonth = s.SalaryMonth,
            DebitAccount = s.DebitAccount,
            Currency = s.Currency,
            CreatedAt = s.CreatedAt,
            PostedAt = s.PostedAt,
            CreatedByUserId = s.CreatedByUserId,
            PostedByUserId = s.PostedByUserId,
            TotalAmount = s.TotalAmount,
            BankReference = s.BankReference,
            BankResponseRaw = s.BankResponseRaw,
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
                IsTransferred = e.IsTransferred
            }).ToList()
        };
    }

    #endregion


}
