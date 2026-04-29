// CompGateApi.Infrastructure/Repositories/EmployeeSalaryRepository.cs
using System.Net.Http.Json;
using AutoMapper;
using ClosedXML.Excel;
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
            EvoWallet = dto.EvoWallet,
            BcdWallet = dto.BcdWallet,
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
            EvoWallet = e.EvoWallet,
            BcdWallet = e.BcdWallet,
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
        e.EvoWallet = dto.EvoWallet;
        e.BcdWallet = dto.BcdWallet;
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
            EvoWallet = e.EvoWallet,
            BcdWallet = e.BcdWallet,
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

    public async Task<EmployeeExcelImportResultDto> ImportEmployeesFromExcelAsync(int companyId, Stream excelStream)
    {
        var result = new EmployeeExcelImportResultDto();

        if (excelStream == null || !excelStream.CanRead)
            throw new InvalidDataException("Invalid Excel file stream.");

        using var workbook = new XLWorkbook(excelStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
            throw new InvalidDataException("The uploaded file does not contain any worksheet.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow == 0)
            return result;

        var now = DateTime.UtcNow;
        var existingEmployees = await _db.Employees
            .Where(e => e.CompanyId == companyId)
            .ToListAsync();

        var byAccount = existingEmployees
            .Where(e => !string.IsNullOrWhiteSpace(e.AccountNumber))
            .ToDictionary(e => e.AccountNumber.Trim(), StringComparer.OrdinalIgnoreCase);

        var seenAccountsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var firstRow = LooksLikeHeaderRow(worksheet.Row(1)) ? 2 : 1;

        for (var rowNumber = firstRow; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            var name = row.Cell(1).GetString().Trim();
            var account = NormalizeImportedAccount(row.Cell(2).GetFormattedString());
            var salaryText = row.Cell(3).GetFormattedString().Trim();

            var rowIsCompletelyEmpty =
                string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(account) &&
                string.IsNullOrWhiteSpace(salaryText);

            if (rowIsCompletelyEmpty)
                continue;

            result.TotalRows++;

            if (string.IsNullOrWhiteSpace(name))
            {
                result.SkippedCount++;
                result.Errors.Add(new EmployeeExcelImportRowErrorDto { RowNumber = rowNumber, Message = "Name (Column A) is required." });
                continue;
            }

            if (string.IsNullOrWhiteSpace(account))
            {
                result.SkippedCount++;
                result.Errors.Add(new EmployeeExcelImportRowErrorDto { RowNumber = rowNumber, Message = "Account number (Column B) is required." });
                continue;
            }

            if (!account.All(char.IsDigit) || account.Length != 13)
            {
                result.SkippedCount++;
                result.Errors.Add(new EmployeeExcelImportRowErrorDto { RowNumber = rowNumber, Message = "Account number (Column B) must be exactly 13 digits." });
                continue;
            }

            if (!TryParseSalary(row.Cell(3), out var salary))
            {
                result.SkippedCount++;
                result.Errors.Add(new EmployeeExcelImportRowErrorDto { RowNumber = rowNumber, Message = "Salary (Column C) is invalid." });
                continue;
            }

            if (salary < 0)
            {
                result.SkippedCount++;
                result.Errors.Add(new EmployeeExcelImportRowErrorDto { RowNumber = rowNumber, Message = "Salary (Column C) cannot be negative." });
                continue;
            }

            if (!seenAccountsInFile.Add(account))
            {
                result.SkippedCount++;
                result.Errors.Add(new EmployeeExcelImportRowErrorDto { RowNumber = rowNumber, Message = "Duplicate account number found in uploaded file." });
                continue;
            }

            var cleanedName = name.Length > 100 ? name[..100] : name;
            var roundedSalary = Math.Round(salary, 3);

            if (byAccount.ContainsKey(account))
            {
                result.SkippedCount++;
                result.Errors.Add(new EmployeeExcelImportRowErrorDto
                {
                    RowNumber = rowNumber,
                    Message = "Account number already exists for this company. Row skipped."
                });
                continue;
            }

            var created = new Employee
            {
                CompanyId = companyId,
                Name = cleanedName,
                Email = BuildImportedEmail(companyId, account),
                Phone = BuildImportedPhone(account),
                Salary = roundedSalary,
                Date = now,
                AccountNumber = account,
                AccountType = "account",
                SendSalary = true,
                CanPost = true
            };

            _db.Employees.Add(created);
            byAccount[account] = created;
            result.CreatedCount++;
        }

        await _db.SaveChangesAsync();
        return result;
    }

    private static bool LooksLikeHeaderRow(IXLRow row)
    {
        var colA = row.Cell(1).GetString().Trim().ToLowerInvariant();
        var colB = row.Cell(2).GetString().Trim().ToLowerInvariant();
        var colC = row.Cell(3).GetString().Trim().ToLowerInvariant();

        return (colA == "name" || colA.Contains("employee"))
            && colB.Contains("account")
            && colC.Contains("salary");
    }

    private static bool TryParseSalary(IXLCell salaryCell, out decimal salary)
    {
        if (salaryCell.TryGetValue<decimal>(out salary))
            return true;

        var raw = salaryCell.GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        raw = raw.Replace(",", "");
        return decimal.TryParse(
            raw,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out salary);
    }

    private static string NormalizeImportedAccount(string rawAccount)
    {
        if (string.IsNullOrWhiteSpace(rawAccount))
            return string.Empty;

        var normalized = rawAccount.Trim().Replace(" ", "");
        if (normalized.EndsWith(".0", StringComparison.Ordinal))
            normalized = normalized[..^2];
        return normalized;
    }

    private static string BuildImportedEmail(int companyId, string account)
    {
        return $"imported-{companyId}-{account}@placeholder.local";
    }

    private static string BuildImportedPhone(string account)
    {
        return account.Length <= 10 ? account : account[^10..];
    }

    public async Task<PagedResult<SalaryCycleDto>> GetSalaryCyclesAsync(int companyId, int page, int limit)
    {
        var query = _db.SalaryCycles
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.CreatedAt)
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
                AdditionalMonth = s.AdditionalMonth,
                DebitAccount = s.DebitAccount,
                Currency = s.Currency,
                CreatedAt = s.CreatedAt,
                PostedAt = s.PostedAt,
                CreatedByUserId = s.CreatedByUserId,
                PostedByUserId = s.PostedByUserId,
                TotalAmount = s.TotalAmount,
                BankReference = s.BankReference,
                BankResponseRaw = s.BankResponseRaw,
                BankBatchHistoryJson = s.BankBatchHistoryJson,
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
                    IsTransferred = e.IsTransferred,
                    TransferResultCode = e.TransferResultCode,
                    TransferResultReason = e.TransferResultReason,
                    TransferredAt = e.TransferredAt
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

        // Round per-currency: LYD -> 3dp, others -> 2dp
        var decimals = string.Equals(dto.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
        list = list.Select(x => (emp: x.emp, amount: Math.Round(x.amount, decimals))).ToList();
        var total = Math.Round(list.Sum(x => x.amount), decimals);
        // normalize additionalMonth: only keep if between 13 and 24 (inclusive)
        string? additionalMonthStr = null;
        if (dto.AdditionalMonth.HasValue && dto.AdditionalMonth.Value >= 13 && dto.AdditionalMonth.Value <= 24)
            additionalMonthStr = dto.AdditionalMonth.Value.ToString();

        var cycle = new SalaryCycle
        {
            CompanyId = companyId,
            SalaryMonth = dto.SalaryMonth,
            AdditionalMonth = additionalMonthStr,
            DebitAccount = dto.DebitAccount,
            Currency = dto.Currency,
            CreatedByUserId = createdByUserId,
            TotalAmount = total,
            Entries = list.Select(x => new SalaryEntry
            {
                EmployeeId = x.emp.Id,
                Amount = Math.Round(x.amount, decimals)
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

        // 5) Build EMPLOYEE credits using PostBatchApply (ONE DEBIT -> MANY CREDITS)
        // LYD uses 3 decimals, otherwise 2
        int SCALE = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 1000 : 100;
        const int PAD = 15;

        var baseId = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var hidEmp = baseId + "BA00";           // batch apply ref
        cycle.BankReference = hidEmp;           // save immediately
        await _db.SaveChangesAsync();

        var journalEntriesEmp = new List<Dictionary<string, string>>();
        int i = 1;

        var salaryMonthText = cycle.SalaryMonth ?? string.Empty;
        var addMonthText = string.IsNullOrWhiteSpace(cycle.AdditionalMonth) ? null : cycle.AdditionalMonth;

        foreach (var e in eligibleEntries)
        {
            var amount = e.Amount;
            var fee = fixedFee.Value;
            if (amount < fee)
                throw new PayrollException($"Salary {amount:0.###} LYD is less than the fixed fee {fee:0.###} LYD (employee #{e.EmployeeId}).");

            var employeeCredit = amount;  // GROSS to employee
            var didEmp = baseId + $"B{i:00}"; i++;

            var je = new Dictionary<string, string>
            {
                ["YBCD10DID"] = didEmp,
                ["YBCD10ACC"] = NormalizeAcc13(e.Employee!.AccountNumber!),
                ["YBCD10AMT"] = ((long)(Math.Round(employeeCredit, SCALE == 1000 ? 3 : 2) * SCALE)).ToString($"D{PAD}"),
                ["YBCD10CCY"] = cycle.Currency,
                ["YBCD10NR1"] = $"مرتبات {salaryMonthText}",
                ["YBCD10NR3"] = string.Empty,
                ["YBCD10NR4"] = string.Empty
            };
            if (addMonthText != null)
                je["YBCD10NR2"] = $"Ù…Ø±ØªØ¨ Ø§Ø¶Ø§ÙÙŠ {salaryMonthText} {addMonthText}";
            journalEntriesEmp.Add(je);
        }

        var customerNumber = debitAcc13.Substring(4, 6);
        var totalEmployeesAmount = journalEntriesEmp.Sum(j => long.Parse(j["YBCD10AMT"]));
        var detailsEmp = new Dictionary<string, object>
        {
            ["@UNIT"] = "LIV",
            ["@HID"] = hidEmp,
            ["@TYPE"] = "D",                // one DEBIT (company) -> many credits (employees)
            ["@FORCPAY"] = "N",
            ["@ACCOUNT"] = debitAcc13,
            ["@TRFAMT"] = totalEmployeesAmount.ToString($"D{PAD}"),
            ["@TRFCCY"] = cycle.Currency,
            ["@DTCD"] = "021",
            ["@CTCD"] = "521",
            ["@TRFREF"] = hidEmp,
            ["@NR1"] = $"مرتبات {salaryMonthText}",
            ["@NR3"] = string.Empty,
            ["@NR4"] = string.Empty,
            ["JournalEntries"] = journalEntriesEmp
        };
        // include @NR2 key always (empty when no additional month)
        detailsEmp["@NR2"] = addMonthText != null ? $"Extra Salary {salaryMonthText} + {addMonthText}" : string.Empty;

        var payloadEmp = new
        {
            Header = new
            {
                system = "CompanyGateway",
                referenceId = hidEmp,
                userName = "CompanyGateway",
                customerNumber = customerNumber,
                language = "AR"
            },
            Details = detailsEmp
        };

        // 6) Call bank for EMPLOYEES batch apply
        var bankCli = _http.CreateClient("BankApi");
        var requestUri = new Uri(bankCli.BaseAddress!, "api/mobile/PostBatchApply");
        var bankResEmp = await bankCli.PostAsJsonAsync(requestUri, payloadEmp);
        var rawEmp = await bankResEmp.Content.ReadAsStringAsync();

        cycle.BankResponseRaw = rawEmp;         // keep raw (success or fail)
        await _db.SaveChangesAsync();

        if (!bankResEmp.IsSuccessStatusCode)
            throw new PayrollException($"Bank HTTP {(int)bankResEmp.StatusCode} at {requestUri}: {rawEmp}");

        // 7) Parse success accounts for employees from BatchApply response
        var successAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var perAccountOutcome = new Dictionary<string, (string? Code, string? Reason, string Raw)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var jdoc = System.Text.Json.JsonDocument.Parse(rawEmp);
            if (jdoc.RootElement.TryGetProperty("Details", out var d1) &&
                d1.TryGetProperty("Lines", out var arr) &&
                arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var resp = item.TryGetProperty("YBCD10RESP", out var rEl) ? rEl.GetString() : null;
                    var cacc = item.TryGetProperty("YBCD10ACC", out var cEl) ? cEl.GetString() : null;
                    var reason = item.TryGetProperty("YBCD10REAS", out var reasEl) ? reasEl.GetString() :
                                 item.TryGetProperty("REASON", out var reas2) ? reas2.GetString() :
                                 item.TryGetProperty("MESSAGE", out var reas3) ? reas3.GetString() : null;
                    var rawItem = item.GetRawText();
                    if (string.Equals(resp, "S", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(cacc))
                    {
                        successAccounts.Add(cacc.Trim());
                    }
                    if (!string.IsNullOrWhiteSpace(cacc))
                    {
                        perAccountOutcome[cacc.Trim()] = (resp, reason, rawItem);
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
            var acc = NormalizeAcc13(e.Employee!.AccountNumber!);
            if (successAccounts.Contains(e.Employee!.AccountNumber!))
            {
                e.IsTransferred = true;
                e.TransferredAt = DateTime.UtcNow;
                e.PostedByUserId = postedBy;
                e.TransferResultCode = "S";
                e.TransferResultReason = null;
                if (perAccountOutcome.TryGetValue(e.Employee.AccountNumber!, out var oc1))
                    e.BankLineResponseRaw = oc1.Raw;
                successGrossTotal += e.Amount; // GROSS salary
                successCount++;
                anySuccess = true;
            }
            else
            {
                // not successful: record code/reason/raw if present
                if (perAccountOutcome.TryGetValue(e.Employee.AccountNumber!, out var oc))
                {
                    e.TransferResultCode = oc.Code;
                    e.TransferResultReason = oc.Reason;
                    e.BankLineResponseRaw = oc.Raw;
                }
            }
        }

        if (!anySuccess)
        {
            // Persist per-entry failure reasons so caller can inspect; do not throw.
            // This lets the API return 200 with cycle + reasons, and keep cycle unposted.
            await _db.SaveChangesAsync();
            return _mapper.Map<SalaryCycleDto>(cycle);
        }

        // Update cycle totals to ONLY successful gross
        cycle.TotalAmount = successGrossTotal;

        // 8) Commission settlement using PostBatchApply (MANY DEBITS -> ONE CREDIT)
        if (successCount > 0)
        {
            var successfulEmployees = eligibleEntries
                .Where(e => successAccounts.Contains(e.Employee!.AccountNumber!))
                .ToList();

            var baseIdFee = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            var hidFee = baseIdFee + "BF00";

            var journalEntriesFee = new List<Dictionary<string, string>>();
            int j = 1;
            foreach (var e in successfulEmployees)
            {
                var empAcc13 = NormalizeAcc13(e.Employee!.AccountNumber!);
                var did = baseIdFee + $"B{j:00}"; j++;
                var line = new Dictionary<string, string>
                {
                    ["YBCD10DID"] = did,
                    ["YBCD10ACC"] = empAcc13,
                    ["YBCD10AMT"] = ((long)(Math.Round(fixedFee.Value, SCALE == 1000 ? 3 : 2) * SCALE)).ToString($"D{PAD}"),
                    ["YBCD10CCY"] = cycle.Currency,
                    ["YBCD10NR1"] = $"عمولة مرتبات {salaryMonthText}",
                    ["YBCD10NR3"] = string.Empty,
                    ["YBCD10NR4"] = string.Empty
                };
                if (addMonthText != null)
                    line["YBCD10NR2"] = $"Ø´Ù‡Ø± Ø§Ø¶Ø§ÙÙŠ {addMonthText}";
                journalEntriesFee.Add(line);
                // Track fee amount on entry for auditing
                e.CommissionAmount = fixedFee.Value;
            }

            var totalFeeAmt = journalEntriesFee.Sum(x => long.Parse(x["YBCD10AMT"]));
            var detailsFee = new Dictionary<string, object>
            {
                ["@UNIT"] = "LIV",
                ["@HID"] = hidFee,
                ["@TYPE"] = "C",            // many debits (employees) -> ONE CREDIT (fee GL)
                ["@FORCPAY"] = "N",
                ["@ACCOUNT"] = feeGl,
                ["@TRFAMT"] = totalFeeAmt.ToString($"D{PAD}"),
                ["@TRFCCY"] = cycle.Currency,
                ["@DTCD"] = "021",
                ["@CTCD"] = "521",
                ["@TRFREF"] = hidFee,
                ["@NR1"] = $"عمولة مرتبات {salaryMonthText}",
                ["@NR3"] = string.Empty,
                ["@NR4"] = string.Empty,
                ["JournalEntries"] = journalEntriesFee
            };
            detailsFee["@NR2"] = addMonthText != null ? $"Extra {addMonthText}" : string.Empty;

            var payloadFee = new
            {
                Header = new
                {
                    system = "CompanyGateway",
                    referenceId = hidFee,
                    userName = "CompanyGateway",
                    customerNumber = customerNumber, // using company context
                    language = "AR"
                },
                Details = detailsFee
            };

            var requestUriFee = new Uri(bankCli.BaseAddress!, "api/mobile/PostBatchApply");
            var bankResFee = await bankCli.PostAsJsonAsync(requestUriFee, payloadFee);
            var rawFee = await bankResFee.Content.ReadAsStringAsync();

            cycle.BankFeeReference = hidFee;
            cycle.BankFeeResponseRaw = rawFee;

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
            AdditionalMonth = s.AdditionalMonth,
            DebitAccount = s.DebitAccount,
            Currency = s.Currency,
            CreatedAt = s.CreatedAt,
            PostedAt = s.PostedAt,
            CreatedByUserId = s.CreatedByUserId,
            PostedByUserId = s.PostedByUserId,
            TotalAmount = s.TotalAmount,
            BankReference = s.BankReference,
            BankResponseRaw = s.BankResponseRaw,
            BankBatchHistoryJson = s.BankBatchHistoryJson,
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
                IsTransferred = en.IsTransferred,
                TransferResultCode = en.TransferResultCode,
                TransferResultReason = en.TransferResultReason,
                TransferredAt = en.TransferredAt
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
            IsTransferred = en.IsTransferred,
            TransferResultCode = en.TransferResultCode,
            TransferResultReason = en.TransferResultReason,
            TransferredAt = en.TransferredAt
        };
    }

    public async Task<List<SalaryEntryDto>> GetFailedEntriesAsync(int companyId, int cycleId)
    {
        var list = await _db.SalaryEntries
            .Include(e => e.Employee)
            .Include(e => e.SalaryCycle)
            .Where(e => e.SalaryCycle.CompanyId == companyId && e.SalaryCycleId == cycleId && !e.IsTransferred)
            .AsNoTracking()
            .ToListAsync();

        return list.Select(en => new SalaryEntryDto
        {
            Id = en.Id,
            EmployeeId = en.EmployeeId,
            Name = en.Employee.Name,
            Email = en.Employee.Email,
            Phone = en.Employee.Phone,
            Salary = en.Amount,
            Date = en.Employee.Date,
            AccountNumber = en.Employee.AccountNumber,
            AccountType = en.Employee.AccountType,
            SendSalary = en.Employee.SendSalary,
            CanPost = en.Employee.CanPost,
            IsTransferred = en.IsTransferred,
            TransferResultCode = en.TransferResultCode,
            TransferResultReason = en.TransferResultReason,
            TransferredAt = en.TransferredAt
        }).ToList();
    }

    public async Task<SalaryEntryDto?> EditEntryAndEmployeeAsync(int companyId, int cycleId, int entryId, SalaryEntryEditDto dto)
    {
        var en = await _db.SalaryEntries
            .Include(e => e.Employee)
            .Include(e => e.SalaryCycle)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.SalaryCycleId == cycleId && e.SalaryCycle.CompanyId == companyId);

        if (en == null) return null;
        if (en.IsTransferred) throw new PayrollException("Cannot edit an entry that has already been transferred.");

        if (dto.Amount.HasValue)
        {
            var cycle = await _db.SalaryCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == en.SalaryCycleId);
            var decimals = (cycle != null && string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase)) ? 3 : 2;
            en.Amount = Math.Round(dto.Amount.Value, decimals);
        }
        if (!string.IsNullOrWhiteSpace(dto.AccountNumber)) en.Employee.AccountNumber = dto.AccountNumber!;
        if (!string.IsNullOrWhiteSpace(dto.AccountType)) en.Employee.AccountType = dto.AccountType!;
        if (!string.IsNullOrWhiteSpace(dto.EvoWallet)) en.Employee.EvoWallet = dto.EvoWallet!;
        if (!string.IsNullOrWhiteSpace(dto.BcdWallet)) en.Employee.BcdWallet = dto.BcdWallet!;

        await _db.SaveChangesAsync();

        return new SalaryEntryDto
        {
            Id = en.Id,
            EmployeeId = en.EmployeeId,
            Name = en.Employee.Name,
            Email = en.Employee.Email,
            Phone = en.Employee.Phone,
            Salary = en.Amount,
            Date = en.Employee.Date,
            AccountNumber = en.Employee.AccountNumber,
            AccountType = en.Employee.AccountType,
            SendSalary = en.Employee.SendSalary,
            CanPost = en.Employee.CanPost,
            IsTransferred = en.IsTransferred,
            TransferResultCode = en.TransferResultCode,
            TransferResultReason = en.TransferResultReason,
            TransferredAt = en.TransferredAt
        };
    }

    public async Task<SalaryCycleDto?> RepostFailedEntriesAsync(int companyId, int cycleId, int postedBy, SalaryRepostIdsRequestDto dto)
    {
        var cycle = await _db.SalaryCycles
            .Include(c => c.Entries).ThenInclude(e => e.Employee)
            .Include(c => c.Company).ThenInclude(c => c.ServicePackage)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.CompanyId == companyId);

        if (cycle is null) return null;

        var pkgId = cycle.Company.ServicePackageId;
        var detail = await _db.ServicePackageDetails
            .Include(d => d.TransactionCategory)
            .FirstOrDefaultAsync(d => d.ServicePackageId == pkgId && d.TransactionCategory.Name == "Salary Payment");
        if (detail is null || !detail.IsEnabledForPackage)
            throw new PayrollException("Salary payments are not enabled for your service package.");

        const int TRXCAT_SALARY_FIXED_FEE = 17;
        var feePricing = await _db.Pricings.AsNoTracking().FirstOrDefaultAsync(p => p.TrxCatId == TRXCAT_SALARY_FIXED_FEE && p.Unit == 1);
        if (feePricing == null) throw new PayrollException("Pricing for salary fixed fee is not configured.");
        var fixedFee = feePricing.Price ?? 0m;
        if (fixedFee <= 0m) throw new PayrollException("Invalid pricing: fixed fee must be greater than zero.");

        var debitAcc13 = NormalizeAcc13(cycle.DebitAccount);
        if (debitAcc13.Length != 13) throw new PayrollException("Debit account must be 13 digits.");
        var feeGl = !string.IsNullOrWhiteSpace(feePricing.GL1) ? NormalizeAcc13(feePricing.GL1) : BuildCommissionGlFromSender(debitAcc13);
        if (feeGl.Length != 13) throw new PayrollException("Configured fee GL (GL1) must be a 13-digit account.");

        int SCALE = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 1000 : 100;
        const int PAD = 15;

        var entryIds = new HashSet<int>((dto.EntryIds ?? new List<int>()));
        var employeeIds = new HashSet<int>((dto.EmployeeIds ?? new List<int>()));
        var toRepost = cycle.Entries
            .Where(e => !e.IsTransferred &&
                        (entryIds.Contains(e.Id) || (employeeIds.Count > 0 && employeeIds.Contains(e.EmployeeId))))
            .ToList();
        if (toRepost.Count == 0) throw new PayrollException("No failed entries to repost were provided (check entryIds/employeeIds).");

        foreach (var e in toRepost)
        {
            e.TransferResultCode = null; e.TransferResultReason = null; e.BankLineResponseRaw = null;
        }
        await _db.SaveChangesAsync();

        var baseId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var hidEmp = baseId + "RA00";
        var salaryMonthText = cycle.SalaryMonth ?? string.Empty;
        var addMonthText = string.IsNullOrWhiteSpace(cycle.AdditionalMonth) ? null : cycle.AdditionalMonth;

        var journalEntriesEmp = new List<Dictionary<string, string>>(); int i = 1;
        foreach (var e in toRepost)
        {
            var acct = NormalizeAcc13(e.Employee.AccountNumber!);
            if (acct.Length != 13) throw new PayrollException($"Employee #{e.EmployeeId} account must be 13 digits.");
            var did = baseId + $"R{i:00}"; i++;
            var line = new Dictionary<string, string>
            {
                ["YBCD10DID"] = did,
                ["YBCD10ACC"] = acct,
                ["YBCD10AMT"] = ((long)(Math.Round(e.Amount, SCALE == 1000 ? 3 : 2) * SCALE)).ToString($"D{PAD}"),
                ["YBCD10CCY"] = cycle.Currency,
                ["YBCD10NR1"] = $"مرتبات {salaryMonthText}",
                ["YBCD10NR3"] = string.Empty,
                ["YBCD10NR4"] = string.Empty
            };
            if (addMonthText != null) line["YBCD10NR2"] = $"Extra Salary {salaryMonthText} + {addMonthText}";
            journalEntriesEmp.Add(line);
        }

        var customerNumber = debitAcc13.Substring(4, 6);
        var totalEmployeesAmount = journalEntriesEmp.Sum(j => long.Parse(j["YBCD10AMT"]));
        var detailsEmp = new Dictionary<string, object>
        {
            ["@UNIT"] = "LIV",
            ["@HID"] = hidEmp,
            ["@TYPE"] = "D",
            ["@FORCPAY"] = "N",
            ["@ACCOUNT"] = debitAcc13,
            ["@TRFAMT"] = totalEmployeesAmount.ToString($"D{PAD}"),
            ["@TRFCCY"] = cycle.Currency,
            ["@DTCD"] = "021",
            ["@CTCD"] = "521",
            ["@TRFREF"] = hidEmp,
            ["@NR1"] = $"مرتبات {salaryMonthText}",
            ["@NR3"] = string.Empty,
            ["@NR4"] = string.Empty,
            ["JournalEntries"] = journalEntriesEmp
        };
        detailsEmp["@NR2"] = addMonthText != null ? $"Extra Salary {salaryMonthText} + {addMonthText}" : string.Empty;

        var payloadEmp = new { Header = new { system = "CompanyGateway", referenceId = hidEmp, userName = "CompanyGateway", customerNumber = customerNumber, language = "AR" }, Details = detailsEmp };
        var bankCli = _http.CreateClient("BankApi");
        var requestUri = new Uri(bankCli.BaseAddress!, "api/mobile/PostBatchApply");
        var bankResEmp = await bankCli.PostAsJsonAsync(requestUri, payloadEmp);
        var rawEmp = await bankResEmp.Content.ReadAsStringAsync();
        cycle.BankResponseRaw = rawEmp; cycle.BankReference = hidEmp; await _db.SaveChangesAsync();
        if (!bankResEmp.IsSuccessStatusCode) throw new PayrollException($"Bank HTTP {(int)bankResEmp.StatusCode} at {requestUri}: {rawEmp}");

        var successAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var perAccountOutcome = new Dictionary<string, (string? Code, string? Reason, string Raw)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var jdoc = System.Text.Json.JsonDocument.Parse(rawEmp);
            if (jdoc.RootElement.TryGetProperty("Details", out var d1) && d1.TryGetProperty("Lines", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var resp = item.TryGetProperty("YBCD10RESP", out var rEl) ? rEl.GetString() : null;
                    var cacc = item.TryGetProperty("YBCD10ACC", out var cEl) ? cEl.GetString() : null;
                    var reason = item.TryGetProperty("YBCD10REAS", out var reasEl) ? reasEl.GetString() : item.TryGetProperty("REASON", out var reas2) ? reas2.GetString() : item.TryGetProperty("MESSAGE", out var reas3) ? reas3.GetString() : null;
                    var rawItem = item.GetRawText();
                    if (!string.IsNullOrWhiteSpace(cacc)) perAccountOutcome[cacc.Trim()] = (resp, reason, rawItem);
                    if (string.Equals(resp, "S", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cacc)) successAccounts.Add(cacc.Trim());
                }
            }
        }
        catch { }

        int successCount = 0; decimal newSuccessGrossTotal = 0m; foreach (var e in toRepost)
        {
            var acct = NormalizeAcc13(e.Employee.AccountNumber!);
            if (successAccounts.Contains(acct) || successAccounts.Contains(e.Employee.AccountNumber!))
            { e.IsTransferred = true; e.TransferredAt = DateTime.UtcNow; e.PostedByUserId = postedBy; e.TransferResultCode = "S"; e.TransferResultReason = null; if (perAccountOutcome.TryGetValue(acct, out var oc1)) e.BankLineResponseRaw = oc1.Raw; successCount++; newSuccessGrossTotal += e.Amount; }
            else { if (perAccountOutcome.TryGetValue(acct, out var oc)) { e.TransferResultCode = oc.Code; e.TransferResultReason = oc.Reason; e.BankLineResponseRaw = oc.Raw; } }
        }

        // Accumulate newly successful gross amounts into cycle total
        if (successCount > 0)
        {
            cycle.TotalAmount += newSuccessGrossTotal;
        }

        string? hidFee = null; string? rawFee = null; if (successCount > 0)
        {
            var baseIdFee = DateTime.UtcNow.ToString("yyyyMMddHHmmss"); hidFee = baseIdFee + "RF00";
            var journalEntriesFee = new List<Dictionary<string, string>>(); int j = 1;
            foreach (var e in toRepost.Where(x => x.IsTransferred))
            {
                var acct = NormalizeAcc13(e.Employee.AccountNumber!);
                var did = baseIdFee + $"R{j:00}"; j++;
                journalEntriesFee.Add(new Dictionary<string, string> { ["YBCD10DID"] = did, ["YBCD10ACC"] = acct, ["YBCD10AMT"] = ((long)(Math.Round(fixedFee, SCALE == 1000 ? 3 : 2) * SCALE)).ToString($"D{PAD}"), ["YBCD10CCY"] = cycle.Currency, ["YBCD10NR1"] = $"عمولة مرتبات {salaryMonthText}", ["YBCD10NR3"] = string.Empty, ["YBCD10NR4"] = string.Empty });
                e.CommissionAmount = fixedFee;
            }
            var totalFeeAmt = journalEntriesFee.Sum(x => long.Parse(x["YBCD10AMT"]));
            var detailsFee = new Dictionary<string, object> { ["@UNIT"] = "LIV", ["@HID"] = hidFee, ["@TYPE"] = "C", ["@FORCPAY"] = "N", ["@ACCOUNT"] = feeGl, ["@TRFAMT"] = totalFeeAmt.ToString($"D{PAD}"), ["@TRFCCY"] = cycle.Currency, ["@DTCD"] = "021", ["@CTCD"] = "521", ["@TRFREF"] = hidFee, ["@NR1"] = $"عمولة مرتبات {salaryMonthText}", ["@NR3"] = string.Empty, ["@NR4"] = string.Empty, ["JournalEntries"] = journalEntriesFee };
            detailsFee["@NR2"] = addMonthText != null ? $"Extra {addMonthText}" : string.Empty;
            var payloadFee = new { Header = new { system = "CompanyGateway", referenceId = hidFee, userName = "CompanyGateway", customerNumber = customerNumber, language = "AR" }, Details = detailsFee };
            var requestUriFee = new Uri(bankCli.BaseAddress!, "api/mobile/PostBatchApply"); var bankResFee = await bankCli.PostAsJsonAsync(requestUriFee, payloadFee); rawFee = await bankResFee.Content.ReadAsStringAsync();
            cycle.BankFeeReference = hidFee; cycle.BankFeeResponseRaw = rawFee;
        }

        try
        {
            var history = new List<System.Text.Json.Nodes.JsonObject>();
            if (!string.IsNullOrWhiteSpace(cycle.BankBatchHistoryJson))
            {
                var arr = System.Text.Json.Nodes.JsonNode.Parse(cycle.BankBatchHistoryJson) as System.Text.Json.Nodes.JsonArray;
                if (arr != null) foreach (var n in arr) if (n is System.Text.Json.Nodes.JsonObject o) history.Add(o);
            }
            var obj = new System.Text.Json.Nodes.JsonObject
            {
                ["attemptAtUtc"] = DateTime.UtcNow.ToString("o"),
                ["entryIds"] = new System.Text.Json.Nodes.JsonArray(toRepost.Select(e => (System.Text.Json.Nodes.JsonNode?)e.Id).ToArray()),
                ["salaryRef"] = hidEmp,
                ["feeRef"] = hidFee
            };
            var arrOut = new System.Text.Json.Nodes.JsonArray(history.Concat(new[] { obj }).ToArray());
            cycle.BankBatchHistoryJson = arrOut.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
        }
        catch { }

        await _db.SaveChangesAsync();
        return await GetSalaryCycleAsync(companyId, cycleId);
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

        if (dto.SalaryMonth is not null) cycle.SalaryMonth = dto.SalaryMonth;
        if (dto.AdditionalMonth is not null) cycle.AdditionalMonth = dto.AdditionalMonth.Value.ToString();
        if (dto.DebitAccount is not null) cycle.DebitAccount = dto.DebitAccount;
        if (dto.Currency is not null) cycle.Currency = dto.Currency;

        var incoming = (dto.Entries ?? Enumerable.Empty<SalaryEntryUpsertDto>())
                       .ToDictionary(e => e.EmployeeId, e => e);


        foreach (var existing in cycle.Entries.ToList())
        {
            if (incoming.TryGetValue(existing.EmployeeId, out var inc))
            {
                // Round per cycle currency precision (LYD=3dp, others=2dp)
                var decimals = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
                existing.Amount = Math.Round(inc.Salary, decimals);
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

            var decimals = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
            cycle.Entries.Add(new SalaryEntry
            {
                EmployeeId = emp.Id,
                Amount = Math.Round(inc.Salary, decimals)
            });
        }

        {
            var decimals = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
            cycle.TotalAmount = Math.Round(cycle.Entries.Sum(e => e.Amount), decimals);
        }

        await _db.SaveChangesAsync();
        return _mapper.Map<SalaryCycleDto>(cycle);
    }

    public async Task<SalaryCycleDto?> AddEntriesToPostedCycleAsync(int companyId, int cycleId, SalaryCycleAddEntriesDto dto)
    {
        var cycle = await _db.SalaryCycles
            .Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.CompanyId == companyId);

        if (cycle == null) return null;
        if (cycle.PostedAt == null)
            throw new PayrollException("Salary cycle is not posted. Use the save endpoint before posting.");

        if (dto.Entries == null || dto.Entries.Count == 0)
            throw new PayrollException("At least one entry is required.");

        var duplicateRequestIds = dto.Entries
            .GroupBy(e => e.EmployeeId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();
        if (duplicateRequestIds.Count > 0)
            throw new PayrollException($"Duplicate employee IDs in request: {string.Join(", ", duplicateRequestIds)}.");

        var existingEmployeeIds = cycle.Entries
            .Select(e => e.EmployeeId)
            .ToHashSet();

        var alreadyInCycle = dto.Entries
            .Select(e => e.EmployeeId)
            .Where(existingEmployeeIds.Contains)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        if (alreadyInCycle.Count > 0)
            throw new PayrollException($"Employee(s) already exist in this salary cycle: {string.Join(", ", alreadyInCycle)}.");

        var requestedEmployeeIds = dto.Entries
            .Select(e => e.EmployeeId)
            .Distinct()
            .ToList();

        var companyEmployeeIds = await _db.Employees
            .Where(e => e.CompanyId == companyId && requestedEmployeeIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync();

        var companyEmployeeSet = companyEmployeeIds.ToHashSet();
        var missingEmployeeIds = requestedEmployeeIds
            .Where(id => !companyEmployeeSet.Contains(id))
            .OrderBy(x => x)
            .ToList();
        if (missingEmployeeIds.Count > 0)
            throw new PayrollException($"Employee(s) not found for this company: {string.Join(", ", missingEmployeeIds)}.");

        var decimals = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;

        foreach (var inc in dto.Entries)
        {
            cycle.Entries.Add(new SalaryEntry
            {
                EmployeeId = inc.EmployeeId,
                Amount = Math.Round(inc.Salary, decimals)
            });
        }

        await _db.SaveChangesAsync();
        return await GetSalaryCycleAsync(companyId, cycleId);
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
        {
            var fromDt = new DateTimeOffset(from.Value.Date, TimeSpan.Zero);
            q = q.Where(s => s.CreatedAt >= fromDt);
        }

        if (to.HasValue)
        {
            var end = new DateTimeOffset(to.Value.Date.AddDays(1), TimeSpan.Zero);
            q = q.Where(s => s.CreatedAt < end);
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
        {
            var fromDt = new DateTimeOffset(from.Value.Date, TimeSpan.Zero);
            q = q.Where(s => s.CreatedAt >= fromDt);
        }

        if (to.HasValue)
        {
            var end = new DateTimeOffset(to.Value.Date.AddDays(1), TimeSpan.Zero);
            q = q.Where(s => s.CreatedAt < end);
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
                AdditionalMonth = s.AdditionalMonth,
                DebitAccount = s.DebitAccount,
                Currency = s.Currency,
                CreatedAt = s.CreatedAt,
                PostedAt = s.PostedAt,
                CreatedByUserId = s.CreatedByUserId,
                PostedByUserId = s.PostedByUserId,
                TotalAmount = s.TotalAmount,
                BankReference = s.BankReference,
                BankBatchHistoryJson = s.BankBatchHistoryJson
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
            AdditionalMonth = s.AdditionalMonth,
            DebitAccount = s.DebitAccount,
            Currency = s.Currency,
            CreatedAt = s.CreatedAt,
            PostedAt = s.PostedAt,
            CreatedByUserId = s.CreatedByUserId,
            PostedByUserId = s.PostedByUserId,
            TotalAmount = s.TotalAmount,
            BankReference = s.BankReference,
            BankResponseRaw = s.BankResponseRaw,
            BankFeeReference = s.BankFeeReference,
            BankFeeResponseRaw = s.BankFeeResponseRaw,
            BankBatchHistoryJson = s.BankBatchHistoryJson,
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
                IsTransferred = e.IsTransferred,
                TransferResultCode = e.TransferResultCode,
                TransferResultReason = e.TransferResultReason,
                TransferredAt = e.TransferredAt
            }).ToList()
        };
    }

    #endregion


}


