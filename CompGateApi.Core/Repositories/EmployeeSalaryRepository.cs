// CompGateApi.Infrastructure/Repositories/EmployeeSalaryRepository.cs
using System.Net.Http.Json;
using System.Text.Json;
using AutoMapper;
using ClosedXML.Excel;
using CompGateApi.Core.Abstractions;
using CompGateApi.Core.Dtos;
using CompGateApi.Core.Errors;
using CompGateApi.Core.Options;
using CompGateApi.Data.Context;
using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class EmployeeSalaryRepository : IEmployeeSalaryRepository
{
    private readonly CompGateApiDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IMapper _mapper;
    private readonly IWalletSalaryProviderClient _walletProvider;
    private readonly WalletSalaryReconciliationOptions _reconciliationOptions;

    public EmployeeSalaryRepository(
        CompGateApiDbContext db,
        IHttpClientFactory http,
        IMapper mapper,
        IWalletSalaryProviderClient walletProvider,
        IOptions<WalletSalaryReconciliationOptions> reconciliationOptions)
    {
        _db = db;
        _http = http;
        _mapper = mapper;
        _walletProvider = walletProvider;
        _reconciliationOptions = reconciliationOptions.Value;
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

    private const string PaymentChannelAccount = "account";
    private const string PaymentChannelEvo = "evo";
    private const string PaymentChannelBcd = "bcd";
    private const string EmptyBankAccount = "0000000000000";
    private const string AllocationStatusPending = "pending";
    private const string AllocationStatusSuccess = "success";
    private const string AllocationStatusFailed = "failed";
    private const string AllocationStatusUnresolved = "unresolved";
    private const string ReconciliationStatusNotRequired = "not_required";
    private const string ReconciliationStatusPending = "pending";
    private const string ReconciliationStatusProcessing = "processing";
    private const string ReconciliationStatusResolved = "resolved";
    private const string ReconciliationStatusManualRequired = "manual_required";
    private const string ReconciliationModePaymentRetry = "payment_retry";
    private const string ReconciliationModeStatusApiFirst = "status_api_first";
    private const string EvoShadowAccount = "0015798000006";
    private const string BcdShadowAccount = "0015798000009";
    private static readonly string[] AllowedPaymentChannels = { PaymentChannelAccount, PaymentChannelEvo, PaymentChannelBcd };

    private static readonly JsonSerializerOptions ProviderJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private sealed record BankLineOutcome(string? Code, string? Reason, string Raw);

    private sealed record CoreBatchResult(
        bool IsHttpSuccess,
        int StatusCode,
        string Raw,
        HashSet<string> SuccessAccounts,
        Dictionary<string, BankLineOutcome> Outcomes,
        string? ReturnCode,
        string? ReturnMessageCode,
        string? ReturnMessage);

    private static int CurrencyDecimals(string? currency) =>
        string.Equals(currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;

    private static int CurrencyScale(string? currency) =>
        string.Equals(currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 1000 : 100;

    private static PayrollException SalaryError(
        string code,
        string messageEn,
        string messageAr,
        object? details = null,
        int status = 400) =>
        new(messageEn, code, messageEn, messageAr, details, status);

    private static PayrollException AllocationTotalMismatchError(
        int employeeId,
        int? salaryCycleId,
        decimal salary,
        decimal allocationTotal) =>
        SalaryError(
            "SALARY_ALLOCATION_TOTAL_MISMATCH",
            "Allocation total must equal salary amount.",
            "يجب أن يساوي مجموع التوزيعات مبلغ الراتب.",
            new
            {
                salaryCycleId,
                employeeId,
                salary,
                allocationTotal
            });

    private static string ToCoreAmount(decimal amount, string? currency)
    {
        const int pad = 15;
        var decimals = CurrencyDecimals(currency);
        var scale = CurrencyScale(currency);
        var minorUnits = (long)(Math.Round(amount, decimals) * scale);
        return minorUnits.ToString($"D{pad}");
    }

    private static string NormalizePaymentChannel(string? channel)
    {
        var normalized = (channel ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            PaymentChannelAccount => PaymentChannelAccount,
            PaymentChannelEvo => PaymentChannelEvo,
            PaymentChannelBcd => PaymentChannelBcd,
            _ => throw SalaryError(
                "INVALID_PAYMENT_CHANNEL",
                "Invalid payment channel.",
                "قناة الدفع غير صالحة.",
                new
                {
                    paymentChannel = channel,
                    allowedValues = AllowedPaymentChannels
                })
        };
    }

    private static bool IsWalletChannel(string channel) =>
        string.Equals(channel, PaymentChannelEvo, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(channel, PaymentChannelBcd, StringComparison.OrdinalIgnoreCase);

    private static string WalletShadowAccount(string channel) =>
        string.Equals(channel, PaymentChannelEvo, StringComparison.OrdinalIgnoreCase)
            ? EvoShadowAccount
            : BcdShadowAccount;

    private static string BuildClientReference(int cycleId, int entryId, string channel) =>
        $"SAL-{cycleId}-ENTRY-{entryId}-{channel.ToUpperInvariant()}";

    private static string ResolveAllocationDestination(Employee employee, string channel, string? requestedDestination)
    {
        var hasRequestedDestination = !string.IsNullOrWhiteSpace(requestedDestination);
        var destination = hasRequestedDestination
            ? requestedDestination!.Trim()
            : channel switch
            {
                PaymentChannelAccount => employee.AccountNumber,
                PaymentChannelEvo => employee.EvoWallet,
                PaymentChannelBcd => employee.BcdWallet,
                _ => null
            };

        if (string.IsNullOrWhiteSpace(destination))
            throw SalaryError(
                "SALARY_ALLOCATION_DESTINATION_REQUIRED",
                "Salary allocation destination is required.",
                "وجهة توزيع الراتب مطلوبة.",
                new
                {
                    employeeId = employee.Id,
                    paymentChannel = channel
                });

        if (string.Equals(channel, PaymentChannelAccount, StringComparison.OrdinalIgnoreCase))
        {
            if (!hasRequestedDestination)
                destination = NormalizeAcc13(destination);

            if (destination.Length != 13 || !destination.All(char.IsDigit))
                throw SalaryError(
                    "SALARY_ACCOUNT_DESTINATION_INVALID",
                    "Account salary allocation destination must be 13 digits.",
                    "يجب أن تكون وجهة حساب الراتب مكونة من 13 رقما.",
                    new
                    {
                        employeeId = employee.Id,
                        paymentChannel = channel,
                        destination
                    });
        }

        return destination;
    }

    private static bool HasRealBankDestination(Employee employee)
    {
        var account = NormalizeAcc13(employee.AccountNumber);
        return account.Length == 13 && account.All(char.IsDigit) && account != EmptyBankAccount;
    }

    private static void AddEmployeeDefaultAllocation(
        List<SalaryEntryAllocation> result,
        SalaryEntry entry,
        Employee employee,
        string channel,
        decimal amount,
        int decimals)
    {
        var roundedAmount = Math.Round(amount, decimals);
        if (roundedAmount <= 0m) return;

        result.Add(new SalaryEntryAllocation
        {
            SalaryEntryId = entry.Id,
            PaymentChannel = channel,
            Amount = roundedAmount,
            Destination = ResolveAllocationDestination(employee, channel, null),
            ClientReference = BuildClientReference(entry.SalaryCycleId, entry.Id, channel),
            Status = AllocationStatusPending
        });
    }

    private static List<SalaryEntryAllocation> BuildEmployeeDefaultAllocations(
        SalaryEntry entry,
        Employee employee,
        int decimals)
    {
        var result = new List<SalaryEntryAllocation>();

        if (HasRealBankDestination(employee))
        {
            AddEmployeeDefaultAllocation(
                result,
                entry,
                employee,
                PaymentChannelAccount,
                employee.AccountAllocationAmount,
                decimals);
        }

        if (!string.IsNullOrWhiteSpace(employee.EvoWallet))
        {
            AddEmployeeDefaultAllocation(
                result,
                entry,
                employee,
                PaymentChannelEvo,
                employee.EvoAllocationAmount,
                decimals);
        }

        if (!string.IsNullOrWhiteSpace(employee.BcdWallet))
        {
            AddEmployeeDefaultAllocation(
                result,
                entry,
                employee,
                PaymentChannelBcd,
                employee.BcdAllocationAmount,
                decimals);
        }

        return result;
    }

    private static List<SalaryEntryAllocation> BuildAllocationsForEntry(
        SalaryEntry entry,
        Employee employee,
        SalaryEntryUpsertDto? dto,
        string currency,
        bool throwIfNoDefault)
    {
        var decimals = CurrencyDecimals(currency);

        if (dto?.Allocations is { Count: > 0 })
        {
            var duplicateChannels = dto.Allocations
                .Select(a => NormalizePaymentChannel(a.PaymentChannel))
                .GroupBy(c => c)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateChannels.Count > 0)
                throw SalaryError(
                    "SALARY_DUPLICATE_ALLOCATION_CHANNEL",
                    "Duplicate salary allocation channel.",
                    "لا يمكن تكرار قناة الدفع لنفس الموظف.",
                    new
                    {
                        employeeId = employee.Id,
                        duplicateChannels
                    });

            var result = new List<SalaryEntryAllocation>();
            foreach (var item in dto.Allocations)
            {
                var channel = NormalizePaymentChannel(item.PaymentChannel);
                var amount = Math.Round(item.Amount, decimals);
                if (amount <= 0m)
                    throw SalaryError(
                        "SALARY_ALLOCATION_AMOUNT_INVALID",
                        "Salary allocation amount must be greater than zero.",
                        "يجب أن يكون مبلغ توزيع الراتب أكبر من صفر.",
                        new
                        {
                            employeeId = employee.Id,
                            paymentChannel = channel,
                            amount
                        });

                result.Add(new SalaryEntryAllocation
                {
                    SalaryEntryId = entry.Id,
                    PaymentChannel = channel,
                    Amount = amount,
                    Destination = ResolveAllocationDestination(employee, channel, item.Destination),
                    ClientReference = BuildClientReference(entry.SalaryCycleId, entry.Id, channel),
                    Status = AllocationStatusPending
                });
            }

            var total = Math.Round(result.Sum(a => a.Amount), decimals);
            if (total != Math.Round(entry.Amount, decimals))
                throw AllocationTotalMismatchError(
                    employee.Id,
                    null,
                    Math.Round(entry.Amount, decimals),
                    total);

            return result;
        }

        var defaultAllocations = BuildEmployeeDefaultAllocations(entry, employee, decimals);
        if (defaultAllocations.Count > 0)
        {
            var defaultTotal = Math.Round(defaultAllocations.Sum(a => a.Amount), decimals);
            if (defaultTotal != Math.Round(entry.Amount, decimals))
                throw AllocationTotalMismatchError(
                    employee.Id,
                    null,
                    Math.Round(entry.Amount, decimals),
                    defaultTotal);

            return defaultAllocations;
        }

        var defaultChannel =
            string.Equals(employee.AccountType, PaymentChannelAccount, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(employee.AccountNumber) &&
            HasRealBankDestination(employee)
                ? PaymentChannelAccount
                : string.Equals(employee.AccountType, "wallet", StringComparison.OrdinalIgnoreCase) &&
                  !string.IsNullOrWhiteSpace(employee.EvoWallet)
                    ? PaymentChannelEvo
                    : string.Equals(employee.AccountType, "wallet", StringComparison.OrdinalIgnoreCase) &&
                      !string.IsNullOrWhiteSpace(employee.BcdWallet)
                        ? PaymentChannelBcd
                        : string.Empty;

        if (string.IsNullOrWhiteSpace(defaultChannel))
        {
            if (throwIfNoDefault)
                throw SalaryError(
                    "SALARY_DEFAULT_DESTINATION_MISSING",
                    "No valid default salary destination exists for this employee.",
                    "لا توجد وجهة راتب افتراضية صالحة للموظف.",
                    new
                    {
                        employeeId = employee.Id
                    });
            return new List<SalaryEntryAllocation>();
        }

        return new List<SalaryEntryAllocation>
        {
            new()
            {
                SalaryEntryId = entry.Id,
                PaymentChannel = defaultChannel,
                Amount = Math.Round(entry.Amount, decimals),
                Destination = ResolveAllocationDestination(employee, defaultChannel, null),
                ClientReference = BuildClientReference(entry.SalaryCycleId, entry.Id, defaultChannel),
                Status = AllocationStatusPending
            }
        };
    }

    private static void ResetAllocationForPosting(SalaryEntryAllocation allocation)
    {
        allocation.Status = AllocationStatusPending;
        allocation.TransferResultCode = null;
        allocation.TransferResultReason = null;
        allocation.ProviderTransactionId = null;
        allocation.CommissionAmount = 0m;
        allocation.RawResponse = null;
        allocation.IsTransferred = false;
        allocation.TransferredAt = null;
        allocation.PostedByUserId = null;
    }

    private static void RefreshEntryStatusFromAllocations(SalaryEntry entry)
    {
        if (entry.Allocations.Count == 0)
            return;

        entry.CommissionAmount = entry.Allocations.Sum(a => a.CommissionAmount);
        entry.IsTransferred = entry.Allocations.All(a => a.IsTransferred);
        entry.TransferredAt = entry.IsTransferred
            ? entry.Allocations.Max(a => a.TransferredAt)
            : null;
        entry.PostedByUserId = entry.IsTransferred
            ? entry.Allocations.FirstOrDefault(a => a.PostedByUserId.HasValue)?.PostedByUserId
            : null;

        if (entry.IsTransferred)
        {
            entry.TransferResultCode = "S";
            entry.TransferResultReason = null;
            return;
        }

        var statuses = entry.Allocations.Select(a => a.Status).ToList();
        if (statuses.Any(s => string.Equals(s, AllocationStatusUnresolved, StringComparison.OrdinalIgnoreCase)))
        {
            entry.TransferResultCode = "UNRESOLVED";
            entry.TransferResultReason = "One or more salary allocations require manual review.";
        }
        else if (statuses.Any(s => string.Equals(s, AllocationStatusSuccess, StringComparison.OrdinalIgnoreCase)))
        {
            entry.TransferResultCode = "PARTIAL";
            entry.TransferResultReason = "One or more salary allocations failed.";
        }
        else
        {
            entry.TransferResultCode = entry.Allocations.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.TransferResultCode))?.TransferResultCode;
            entry.TransferResultReason = entry.Allocations.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.TransferResultReason))?.TransferResultReason;
        }
    }

    public async Task<PagedResult<EmployeeDto>> GetAllEmployeesAsync(int companyId, string? searchTerm, int page, int limit)
    {
        var query = _db.Employees.Where(e => e.CompanyId == companyId && !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(e =>
                e.Name.Contains(searchTerm) ||
                (e.Email != null && e.Email.Contains(searchTerm)) ||
                (e.Phone != null && e.Phone.Contains(searchTerm)));
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
                EvoWallet = e.EvoWallet,
                BcdWallet = e.BcdWallet,
                AccountAllocationAmount = e.AccountAllocationAmount,
                EvoAllocationAmount = e.EvoAllocationAmount,
                BcdAllocationAmount = e.BcdAllocationAmount,
                SendSalary = e.SendSalary,
                CanPost = e.CanPost,
                IsDeleted = e.IsDeleted
            }).ToList()
        };
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(int companyId, EmployeeCreateDto dto)
    {
        var e = new Employee
        {
            CompanyId = companyId,
            Name = dto.Name,
            Email = NormalizeOptionalText(dto.Email),
            Phone = NormalizeOptionalText(dto.Phone),
            Salary = dto.Salary,
            Date = dto.Date,
            AccountNumber = dto.AccountNumber,
            AccountType = dto.AccountType,
            EvoWallet = dto.EvoWallet,
            BcdWallet = dto.BcdWallet,
            AccountAllocationAmount = Math.Round(dto.AccountAllocationAmount, 3),
            EvoAllocationAmount = Math.Round(dto.EvoAllocationAmount, 3),
            BcdAllocationAmount = Math.Round(dto.BcdAllocationAmount, 3),
            SendSalary = dto.SendSalary,
            CanPost = dto.CanPost,
            IsDeleted = false
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
            AccountAllocationAmount = e.AccountAllocationAmount,
            EvoAllocationAmount = e.EvoAllocationAmount,
            BcdAllocationAmount = e.BcdAllocationAmount,
            SendSalary = e.SendSalary,
            CanPost = e.CanPost,
            IsDeleted = e.IsDeleted
        };
    }

    public async Task<EmployeeDto?> UpdateEmployeeAsync(int companyId, int id, EmployeeCreateDto dto)
    {
        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (e == null) return null;

        e.Name = dto.Name;
        e.Email = NormalizeOptionalText(dto.Email);
        e.Phone = NormalizeOptionalText(dto.Phone);
        e.Salary = dto.Salary;
        e.Date = dto.Date;
        e.AccountNumber = dto.AccountNumber;
        e.AccountType = dto.AccountType;
        e.EvoWallet = dto.EvoWallet;
        e.BcdWallet = dto.BcdWallet;
        e.AccountAllocationAmount = Math.Round(dto.AccountAllocationAmount, 3);
        e.EvoAllocationAmount = Math.Round(dto.EvoAllocationAmount, 3);
        e.BcdAllocationAmount = Math.Round(dto.BcdAllocationAmount, 3);
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
            AccountAllocationAmount = e.AccountAllocationAmount,
            EvoAllocationAmount = e.EvoAllocationAmount,
            BcdAllocationAmount = e.BcdAllocationAmount,
            SendSalary = e.SendSalary,
            CanPost = e.CanPost,
            IsDeleted = e.IsDeleted
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
        var employees = await _db.Employees.Where(e => e.CompanyId == companyId && !e.IsDeleted && ids.Contains(e.Id)).ToListAsync();

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
            .ToDictionary(e => NormalizeImportedAccount(e.AccountNumber), StringComparer.OrdinalIgnoreCase);

        var seenAccountsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accountsPresentInExcel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            accountsPresentInExcel.Add(account);

            if (string.IsNullOrWhiteSpace(name))
            {
                result.SkippedCount++;
                result.Errors.Add(new EmployeeExcelImportRowErrorDto { RowNumber = rowNumber, Message = "Name (Column A) is required." });
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

            if (byAccount.TryGetValue(account, out var existing))
            {
                existing.Name = cleanedName;
                existing.Salary = roundedSalary;
                existing.Date = now;
                existing.AccountType = "account";
                existing.AccountAllocationAmount = roundedSalary;
                existing.EvoAllocationAmount = 0m;
                existing.BcdAllocationAmount = 0m;
                existing.SendSalary = true;
                existing.CanPost = true;
                existing.IsDeleted = false;
                result.UpdatedCount++;
                continue;
            }

            var created = new Employee
            {
                CompanyId = companyId,
                Name = cleanedName,
                Email = null,
                Phone = null,
                Salary = roundedSalary,
                Date = now,
                AccountNumber = account,
                AccountType = "account",
                AccountAllocationAmount = roundedSalary,
                EvoAllocationAmount = 0m,
                BcdAllocationAmount = 0m,
                SendSalary = true,
                CanPost = true,
                IsDeleted = false
            };

            _db.Employees.Add(created);
            byAccount[account] = created;
            result.CreatedCount++;
        }

        foreach (var existing in existingEmployees)
        {
            var existingAccount = NormalizeImportedAccount(existing.AccountNumber);
            if (string.IsNullOrWhiteSpace(existingAccount))
                continue;

            if (!accountsPresentInExcel.Contains(existingAccount) && !existing.IsDeleted)
            {
                existing.IsDeleted = true;
                result.DeletedCount++;
            }
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

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private async Task EnsureAllocationsForCycleAsync(SalaryCycle cycle)
    {
        var anyCreated = false;
        foreach (var entry in cycle.Entries)
        {
            if (entry.Employee == null || entry.Employee.IsDeleted)
                continue;

            if (entry.Allocations.Count == 0)
            {
                foreach (var allocation in BuildAllocationsForEntry(entry, entry.Employee, null, cycle.Currency, throwIfNoDefault: false))
                {
                    entry.Allocations.Add(allocation);
                    anyCreated = true;
                }
                continue;
            }

            foreach (var allocation in entry.Allocations)
            {
                allocation.PaymentChannel = NormalizePaymentChannel(allocation.PaymentChannel);
                allocation.Destination = ResolveAllocationDestination(entry.Employee, allocation.PaymentChannel, allocation.Destination);
                allocation.ClientReference = BuildClientReference(cycle.Id, entry.Id, allocation.PaymentChannel);
            }

            var decimals = CurrencyDecimals(cycle.Currency);
            var allocationTotal = Math.Round(entry.Allocations.Sum(a => a.Amount), decimals);
            if (allocationTotal != Math.Round(entry.Amount, decimals))
                throw AllocationTotalMismatchError(
                    entry.EmployeeId,
                    cycle.Id,
                    Math.Round(entry.Amount, decimals),
                    allocationTotal);
        }

        if (anyCreated)
            await _db.SaveChangesAsync();
    }

    private static CoreBatchResult ParseCoreBatchResult(bool httpSuccess, int statusCode, string raw, IEnumerable<string> fallbackAccounts)
    {
        var successAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outcomes = new Dictionary<string, BankLineOutcome>(StringComparer.OrdinalIgnoreCase);
        string? returnCodeText = null;
        string? returnMessageCodeText = null;
        string? returnMessageText = null;

        try
        {
            using var jdoc = JsonDocument.Parse(raw);
            var hasHeader = jdoc.RootElement.TryGetProperty("Header", out var header);
            if (hasHeader)
            {
                returnCodeText = header.TryGetProperty("ReturnCode", out var returnCode) ? returnCode.GetString() : null;
                returnMessageCodeText = header.TryGetProperty("ReturnMessageCode", out var returnMessageCode) ? returnMessageCode.GetString() : null;
                returnMessageText = header.TryGetProperty("ReturnMessage", out var returnMessage) ? returnMessage.GetString() : null;
            }

            if (jdoc.RootElement.TryGetProperty("Details", out var details) &&
                details.TryGetProperty("Lines", out var lines) &&
                lines.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in lines.EnumerateArray())
                {
                    var response = item.TryGetProperty("YBCD10RESP", out var responseElement) ? responseElement.GetString() : null;
                    var account = item.TryGetProperty("YBCD10ACC", out var accountElement) ? accountElement.GetString() : null;
                    var reason = item.TryGetProperty("YBCD10REAS", out var reasonElement) ? reasonElement.GetString() :
                                 item.TryGetProperty("YBCD10RESD", out var reasonDescription) ? reasonDescription.GetString() :
                                 item.TryGetProperty("REASON", out var reason2) ? reason2.GetString() :
                                 item.TryGetProperty("MESSAGE", out var reason3) ? reason3.GetString() : null;
                    if (string.IsNullOrWhiteSpace(account))
                        continue;

                    var normalizedAccount = NormalizeAcc13(account);
                    var rawLine = item.GetRawText();
                    outcomes[normalizedAccount] = new BankLineOutcome(response, reason, rawLine);

                    if (string.Equals(response, "S", StringComparison.OrdinalIgnoreCase))
                        successAccounts.Add(normalizedAccount);
                }
            }

            if (outcomes.Count == 0 &&
                string.Equals(returnCodeText, "Success", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var account in fallbackAccounts)
                    successAccounts.Add(NormalizeAcc13(account));
            }
        }
        catch
        {
            // Keep the raw response saved; callers will treat missing line success as failed.
        }

        return new CoreBatchResult(httpSuccess, statusCode, raw, successAccounts, outcomes, returnCodeText, returnMessageCodeText, returnMessageText);
    }

    private async Task<CoreBatchResult> PostCoreBatchAsync(object payload, IEnumerable<string> expectedCreditAccounts)
    {
        var bankClient = _http.CreateClient("BankApi");
        var requestUri = new Uri(bankClient.BaseAddress!, "api/mobile/PostBatchApply");
        var response = await bankClient.PostAsJsonAsync(requestUri, payload);
        var raw = await response.Content.ReadAsStringAsync();
        return ParseCoreBatchResult(response.IsSuccessStatusCode, (int)response.StatusCode, raw, expectedCreditAccounts);
    }

    private static object BuildCoreBatchPayload(
        string hid,
        string type,
        string debitOrCreditAccount,
        string currency,
        string customerNumber,
        string narration,
        List<Dictionary<string, string>> journalEntries)
    {
        const int pad = 15;
        var totalAmount = journalEntries.Sum(j => long.Parse(j["YBCD10AMT"]));
        var details = new Dictionary<string, object>
        {
            ["@UNIT"] = "LIV",
            ["@HID"] = hid,
            ["@TYPE"] = type,
            ["@FORCPAY"] = "N",
            ["@ACCOUNT"] = debitOrCreditAccount,
            ["@TRFAMT"] = totalAmount.ToString($"D{pad}"),
            ["@TRFCCY"] = currency,
            ["@DTCD"] = "021",
            ["@CTCD"] = "521",
            ["@TRFREF"] = hid,
            ["@NR1"] = narration,
            ["@NR2"] = string.Empty,
            ["@NR3"] = string.Empty,
            ["@NR4"] = string.Empty,
            ["JournalEntries"] = journalEntries
        };

        return new
        {
            Header = new
            {
                system = "CompanyGateway",
                referenceId = hid,
                userName = "CompanyGateway",
                customerNumber,
                language = "AR"
            },
            Details = details
        };
    }

    private async Task ReverseSuccessfulBankAllocationsAsync(
        SalaryCycle cycle,
        List<SalaryEntryAllocation> allocations,
        string debitAcc13,
        string customerNumber,
        string salaryMonthText)
    {
        if (allocations.Count == 0)
            return;

        var baseId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var hid = baseId + "AR00";
        var journalEntries = new List<Dictionary<string, string>>();
        var lineNo = 1;

        foreach (var allocation in allocations)
        {
            var destination = NormalizeAcc13(allocation.Destination);
            journalEntries.Add(new Dictionary<string, string>
            {
                ["YBCD10DID"] = baseId + $"A{lineNo:00}",
                ["YBCD10ACC"] = destination,
                ["YBCD10AMT"] = ToCoreAmount(allocation.Amount, cycle.Currency),
                ["YBCD10CCY"] = cycle.Currency,
                ["YBCD10NR1"] = $"Salary reversal {salaryMonthText}",
                ["YBCD10NR2"] = string.Empty,
                ["YBCD10NR3"] = string.Empty,
                ["YBCD10NR4"] = string.Empty
            });
            lineNo++;
        }

        var payload = BuildCoreBatchPayload(
            hid,
            "C",
            debitAcc13,
            cycle.Currency,
            customerNumber,
            $"Salary reversal {salaryMonthText}",
            journalEntries);

        var result = await PostCoreBatchAsync(payload, allocations.Select(a => NormalizeAcc13(a.Destination)));

        foreach (var allocation in allocations)
        {
            var destination = NormalizeAcc13(allocation.Destination);
            var reversed = result.IsHttpSuccess && result.SuccessAccounts.Contains(destination);

            allocation.Status = AllocationStatusFailed;
            allocation.TransferResultCode = reversed ? "SHADOW_FUNDING_FAILED" : "BANK_REVERSAL_FAILED";
            allocation.TransferResultReason = reversed
                ? "Wallet shadow funding failed; bank salary allocation was reversed to cancel the salary posting."
                : "Wallet shadow funding failed, but reversing this bank salary allocation was not confirmed.";
            allocation.IsTransferred = false;
            allocation.TransferredAt = null;
            allocation.PostedByUserId = null;
            allocation.CommissionAmount = 0m;
        }
    }

    private async Task ReverseWalletAmountAsync(
        SalaryWalletBatch batch,
        SalaryCycle cycle,
        string fromShadowAccount,
        decimal amount,
        string reason)
    {
        if (amount <= 0m)
        {
            batch.ReversalStatus = "not_required";
            return;
        }

        var debitAcc13 = NormalizeAcc13(cycle.DebitAccount);
        var shadowAcc13 = NormalizeAcc13(fromShadowAccount);
        var baseId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var hid = baseId + (string.Equals(batch.WalletChannel, PaymentChannelEvo, StringComparison.OrdinalIgnoreCase) ? "ER00" : "BR00");

        var journalEntries = new List<Dictionary<string, string>>
        {
            new()
            {
                ["YBCD10DID"] = baseId + "R01",
                ["YBCD10ACC"] = debitAcc13,
                ["YBCD10AMT"] = ToCoreAmount(amount, cycle.Currency),
                ["YBCD10CCY"] = cycle.Currency,
                ["YBCD10NR1"] = reason,
                ["YBCD10NR2"] = string.Empty,
                ["YBCD10NR3"] = string.Empty,
                ["YBCD10NR4"] = string.Empty
            }
        };

        var payload = BuildCoreBatchPayload(
            hid,
            "D",
            shadowAcc13,
            cycle.Currency,
            shadowAcc13.Substring(4, 6),
            reason,
            journalEntries);

        batch.ReversalRequired = true;
        batch.ReversalAmount = amount;
        batch.ReversalStatus = "pending";
        batch.ReversalBankReference = hid;
        batch.ReversalRequestJson = JsonSerializer.Serialize(payload);

        var result = await PostCoreBatchAsync(payload, new[] { debitAcc13 });
        batch.ReversalResponseJson = result.Raw;

        if (!result.IsHttpSuccess)
        {
            batch.ReversalStatus = "failed";
            batch.ReversalErrorMessage = $"Bank HTTP {result.StatusCode}";
            return;
        }

        batch.ReversalStatus = result.SuccessAccounts.Contains(debitAcc13) ? "success" : "failed";
        if (batch.ReversalStatus == "failed")
            batch.ReversalErrorMessage = result.Outcomes.TryGetValue(debitAcc13, out var outcome)
                ? outcome.Reason
                : "Wallet reversal was not confirmed by core response.";
        else
            batch.ReversedAt = DateTime.UtcNow;
    }

    private WalletSalaryChannelReconciliationOptions ChannelReconciliationOptions(string channel)
    {
        return _reconciliationOptions.ForChannel(channel);
    }

    private string ReconciliationModeFor(string channel)
    {
        var configured = ChannelReconciliationOptions(channel).ReconciliationMode;
        return string.IsNullOrWhiteSpace(configured)
            ? ReconciliationModePaymentRetry
            : configured.Trim().ToLowerInvariant();
    }

    private int MaxAttemptsFor(string channel)
    {
        var configured = ChannelReconciliationOptions(channel).MaxAttempts;
        return Math.Max(1, configured ?? _reconciliationOptions.MaxAttempts);
    }

    private int RetryDelaySecondsFor(string channel)
    {
        var configured = ChannelReconciliationOptions(channel).RetryDelaySeconds;
        return Math.Max(10, configured ?? _reconciliationOptions.RetryDelaySeconds);
    }

    private static WalletSalaryTransferRequestDto BuildWalletTransferRequest(
        SalaryCycle cycle,
        string channel,
        string batchReference,
        string coreReferenceId,
        List<SalaryEntryAllocation> allocations)
    {
        return new WalletSalaryTransferRequestDto
        {
            BatchReference = batchReference,
            CoreReferenceId = coreReferenceId,
            WalletChannel = channel,
            Currency = cycle.Currency,
            RequestedTotalAmount = allocations.Sum(a => a.Amount),
            Items = allocations.Select(a => new WalletSalaryTransferItemDto
            {
                ClientReference = a.ClientReference,
                SalaryCycleId = cycle.Id,
                SalaryEntryId = a.SalaryEntryId,
                EmployeeId = a.SalaryEntry.EmployeeId,
                WalletId = a.Destination,
                Amount = a.Amount,
                Currency = cycle.Currency
            }).ToList()
        };
    }

    private static WalletSalaryStatusRequestDto BuildWalletStatusRequest(WalletSalaryTransferRequestDto request)
    {
        return new WalletSalaryStatusRequestDto
        {
            BatchReference = request.BatchReference,
            CoreReferenceId = request.CoreReferenceId,
            WalletChannel = request.WalletChannel,
            Currency = request.Currency,
            Items = request.Items.Select(i => new WalletSalaryStatusItemDto
            {
                ClientReference = i.ClientReference,
                SalaryCycleId = i.SalaryCycleId,
                SalaryEntryId = i.SalaryEntryId,
                EmployeeId = i.EmployeeId
            }).ToList()
        };
    }

    private static WalletSalaryTransferRequestDto? TryReadWalletTransferRequest(SalaryWalletBatch batch)
    {
        if (string.IsNullOrWhiteSpace(batch.ProviderRequestJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<WalletSalaryTransferRequestDto>(batch.ProviderRequestJson, ProviderJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private List<SalaryEntryAllocation> GetBatchAllocations(SalaryWalletBatch batch)
    {
        var request = TryReadWalletTransferRequest(batch);
        var references = request?.Items
            .Select(i => i.ClientReference)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allAllocations = batch.SalaryCycle.Entries
            .SelectMany(e => e.Allocations)
            .Where(a => string.Equals(a.PaymentChannel, batch.WalletChannel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return references is { Count: > 0 }
            ? allAllocations.Where(a => references.Contains(a.ClientReference)).ToList()
            : allAllocations;
    }

    private void ScheduleWalletBatchReconciliation(
        SalaryWalletBatch batch,
        List<SalaryEntryAllocation> allocations,
        string code,
        string reason)
    {
        var now = DateTime.UtcNow;
        batch.OverallStatus = AllocationStatusUnresolved;
        batch.ReconciliationStatus = ReconciliationStatusPending;
        batch.ReconciliationMode = ReconciliationModeFor(batch.WalletChannel);
        batch.MaxAttempts = MaxAttemptsFor(batch.WalletChannel);
        batch.NextAttemptAt = now.AddSeconds(RetryDelaySecondsFor(batch.WalletChannel));
        batch.LastErrorMessage = reason;
        batch.LockedBy = null;
        batch.LockedUntil = null;
        batch.ProviderErrorMessage = reason;

        foreach (var allocation in allocations.Where(a => string.Equals(a.Status, AllocationStatusUnresolved, StringComparison.OrdinalIgnoreCase)))
        {
            allocation.TransferResultCode = string.IsNullOrWhiteSpace(allocation.TransferResultCode) ? code : allocation.TransferResultCode;
            allocation.TransferResultReason = string.IsNullOrWhiteSpace(allocation.TransferResultReason) ? reason : allocation.TransferResultReason;
        }
    }

    private async Task<bool> ApplyWalletProviderResponseAsync(
        SalaryWalletBatch batch,
        List<SalaryEntryAllocation> allocations,
        WalletSalaryTransferResponseDto response,
        int? postedBy,
        bool reverseWhenFinal)
    {
        batch.ProviderResponseJson = JsonSerializer.Serialize(response, ProviderJsonOptions);
        batch.OverallStatus = response.OverallStatus;
        batch.SuccessfulTotalAmount = response.SuccessfulTotalAmount;
        batch.FailedTotalAmount = response.FailedTotalAmount;
        batch.TotalCommission = response.TotalCommission;
        batch.ProcessedAt = DateTime.UtcNow;

        var resultByReference = response.Results.ToDictionary(r => r.ClientReference, StringComparer.OrdinalIgnoreCase);
        var hasUnresolved = false;

        foreach (var allocation in allocations)
        {
            if (!resultByReference.TryGetValue(allocation.ClientReference, out var result))
            {
                allocation.Status = AllocationStatusUnresolved;
                allocation.TransferResultCode = "MISSING_PROVIDER_RESULT";
                allocation.TransferResultReason = "Wallet provider response did not include this salary allocation.";
                hasUnresolved = true;
                continue;
            }

            allocation.RawResponse = JsonSerializer.Serialize(result, ProviderJsonOptions);
            allocation.TransferResultCode = result.StatusCode;
            allocation.TransferResultReason = result.StatusMessage;
            allocation.ProviderTransactionId = result.ProviderTransactionId;
            allocation.CommissionAmount = string.Equals(result.Status, AllocationStatusSuccess, StringComparison.OrdinalIgnoreCase)
                ? result.Commission
                : 0m;

            if (string.Equals(result.Status, AllocationStatusSuccess, StringComparison.OrdinalIgnoreCase))
            {
                allocation.Status = AllocationStatusSuccess;
                allocation.IsTransferred = true;
                allocation.TransferredAt = result.ProcessedAt == default ? DateTime.UtcNow : result.ProcessedAt;
                allocation.PostedByUserId = postedBy;
            }
            else if (string.Equals(result.Status, AllocationStatusFailed, StringComparison.OrdinalIgnoreCase))
            {
                allocation.Status = AllocationStatusFailed;
                allocation.IsTransferred = false;
                allocation.TransferredAt = null;
                allocation.PostedByUserId = null;
            }
            else
            {
                allocation.Status = AllocationStatusUnresolved;
                allocation.IsTransferred = false;
                allocation.TransferredAt = null;
                allocation.PostedByUserId = null;
                hasUnresolved = true;
            }
        }

        foreach (var entry in batch.SalaryCycle.Entries)
            RefreshEntryStatusFromAllocations(entry);

        if (hasUnresolved)
        {
            batch.OverallStatus = AllocationStatusUnresolved;
            return true;
        }

        var failedTotal = allocations
            .Where(a => string.Equals(a.Status, AllocationStatusFailed, StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.Amount);

        if (reverseWhenFinal &&
            failedTotal > 0m &&
            string.Equals(batch.ReversalStatus, "not_required", StringComparison.OrdinalIgnoreCase))
        {
            await ReverseWalletAmountAsync(batch, batch.SalaryCycle, batch.ShadowAccount, failedTotal, $"Wallet salary reversal {batch.WalletChannel.ToUpperInvariant()}");
        }

        batch.ReconciliationStatus = batch.AttemptCount > 0
            ? ReconciliationStatusResolved
            : ReconciliationStatusNotRequired;
        batch.ResolvedAt = batch.AttemptCount > 0 ? DateTime.UtcNow : null;
        batch.NextAttemptAt = null;
        batch.LockedBy = null;
        batch.LockedUntil = null;

        var successTotal = batch.SalaryCycle.Entries
            .SelectMany(e => e.Allocations)
            .Where(a => a.IsTransferred)
            .Sum(a => a.Amount);

        if (successTotal > 0m)
        {
            batch.SalaryCycle.TotalAmount = Math.Round(successTotal, CurrencyDecimals(batch.SalaryCycle.Currency));
            batch.SalaryCycle.PostedAt ??= DateTime.UtcNow;
            batch.SalaryCycle.PostedByUserId ??= postedBy;
        }

        await CloseManualReviewIfAnyAsync(batch, "Resolved by automatic reconciliation.");
        return false;
    }

    private async Task CloseManualReviewIfAnyAsync(SalaryWalletBatch batch, string note)
    {
        var review = await _db.SalaryWalletManualReviews
            .FirstOrDefaultAsync(r => r.SalaryWalletBatchId == batch.Id && r.Status == "open");
        if (review == null)
            return;

        review.Status = "resolved";
        review.ResolvedAt = DateTime.UtcNow;
        review.ResolutionNote = note;
        review.AttemptCount = batch.AttemptCount;
        review.LastAttemptAt = batch.LastAttemptAt;
        review.LastErrorMessage = batch.LastErrorMessage;
        review.ProviderResponseJson = batch.ProviderResponseJson;
    }

    private async Task EnsureManualReviewAsync(
        SalaryWalletBatch batch,
        List<SalaryEntryAllocation> allocations,
        string reasonCode,
        string reasonMessage)
    {
        var unresolvedAmount = allocations
            .Where(a => string.Equals(a.Status, AllocationStatusUnresolved, StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.Amount);
        if (unresolvedAmount <= 0m)
            unresolvedAmount = allocations.Sum(a => a.Amount);

        var review = await _db.SalaryWalletManualReviews
            .FirstOrDefaultAsync(r => r.SalaryWalletBatchId == batch.Id && r.Status == "open");

        if (review == null)
        {
            review = new SalaryWalletManualReview
            {
                SalaryWalletBatchId = batch.Id,
                SalaryCycleId = batch.SalaryCycleId,
                WalletChannel = batch.WalletChannel,
                BatchReference = batch.BatchReference,
                CoreReferenceId = batch.CoreReferenceId,
                ShadowAccount = batch.ShadowAccount,
                Status = "open"
            };
            _db.SalaryWalletManualReviews.Add(review);
        }

        review.RequestedAmount = batch.RequestedTotalAmount;
        review.UnresolvedAmount = unresolvedAmount;
        review.ReasonCode = reasonCode;
        review.ReasonMessage = reasonMessage;
        review.AttemptCount = batch.AttemptCount;
        review.LastAttemptAt = batch.LastAttemptAt;
        review.LastErrorMessage = batch.LastErrorMessage;
        review.ProviderRequestJson = batch.ProviderRequestJson;
        review.ProviderResponseJson = batch.ProviderResponseJson;
    }

    private async Task MarkReconciliationUnknownAsync(
        SalaryWalletBatch batch,
        List<SalaryEntryAllocation> allocations,
        string code,
        string reason)
    {
        batch.LastErrorMessage = reason;
        batch.ProviderErrorMessage = reason;
        batch.LockedBy = null;
        batch.LockedUntil = null;

        if (batch.AttemptCount >= batch.MaxAttempts)
        {
            batch.ReconciliationStatus = ReconciliationStatusManualRequired;
            batch.NextAttemptAt = null;
            await EnsureManualReviewAsync(batch, allocations, code, reason);
            return;
        }

        batch.ReconciliationStatus = ReconciliationStatusPending;
        batch.NextAttemptAt = DateTime.UtcNow.AddSeconds(RetryDelaySecondsFor(batch.WalletChannel));
    }

    public async Task<int> ReconcilePendingWalletBatchesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var batches = await _db.SalaryWalletBatches
            .Include(b => b.SalaryCycle).ThenInclude(c => c.Entries).ThenInclude(e => e.Employee)
            .Include(b => b.SalaryCycle).ThenInclude(c => c.Entries).ThenInclude(e => e.Allocations)
            .Where(b =>
                (b.ReconciliationStatus == ReconciliationStatusPending ||
                 (b.ReconciliationStatus == ReconciliationStatusProcessing && b.LockedUntil != null && b.LockedUntil <= now)) &&
                (b.NextAttemptAt == null || b.NextAttemptAt <= now))
            .OrderBy(b => b.NextAttemptAt)
            .ThenBy(b => b.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            batch.ReconciliationStatus = ReconciliationStatusProcessing;
            batch.LockedBy = Environment.MachineName;
            batch.LockedUntil = DateTime.UtcNow.AddMinutes(Math.Max(1, _reconciliationOptions.LockMinutes));
            await _db.SaveChangesAsync(cancellationToken);

            await ReconcileWalletBatchAsync(batch, cancellationToken);
            processed++;
        }

        return processed;
    }

    private async Task ReconcileWalletBatchAsync(SalaryWalletBatch batch, CancellationToken cancellationToken)
    {
        var allocations = GetBatchAllocations(batch);
        var request = TryReadWalletTransferRequest(batch) ??
            BuildWalletTransferRequest(batch.SalaryCycle, batch.WalletChannel, batch.BatchReference, batch.CoreReferenceId, allocations);

        batch.ProviderRequestJson ??= JsonSerializer.Serialize(request, ProviderJsonOptions);
        batch.AttemptCount += 1;
        batch.MaxAttempts = batch.MaxAttempts <= 0 ? MaxAttemptsFor(batch.WalletChannel) : batch.MaxAttempts;
        batch.LastAttemptAt = DateTime.UtcNow;

        var mode = string.IsNullOrWhiteSpace(batch.ReconciliationMode)
            ? ReconciliationModeFor(batch.WalletChannel)
            : batch.ReconciliationMode;
        var attemptType = string.Equals(mode, ReconciliationModeStatusApiFirst, StringComparison.OrdinalIgnoreCase)
            ? "status_check"
            : "payment_retry";

        var attempt = new SalaryWalletBatchAttempt
        {
            SalaryWalletBatchId = batch.Id,
            AttemptNumber = batch.AttemptCount,
            AttemptType = attemptType,
            StartedAt = DateTime.UtcNow,
            RequestJson = attemptType == "status_check"
                ? JsonSerializer.Serialize(BuildWalletStatusRequest(request), ProviderJsonOptions)
                : JsonSerializer.Serialize(request, ProviderJsonOptions)
        };
        _db.SalaryWalletBatchAttempts.Add(attempt);

        try
        {
            WalletSalaryTransferResponseDto response;
            if (attemptType == "status_check")
            {
                response = await _walletProvider.CheckSalaryWalletBatchStatusAsync(BuildWalletStatusRequest(request), cancellationToken);
            }
            else
            {
                response = await _walletProvider.PostSalaryWalletBatchAsync(request, cancellationToken);
            }

            attempt.ResponseJson = JsonSerializer.Serialize(response, ProviderJsonOptions);
            attempt.CompletedAt = DateTime.UtcNow;

            var stillUnresolved = await ApplyWalletProviderResponseAsync(
                batch,
                allocations,
                response,
                batch.PostedByUserId ?? batch.SalaryCycle.PostedByUserId ?? batch.SalaryCycle.CreatedByUserId,
                reverseWhenFinal: true);

            if (stillUnresolved)
            {
                attempt.ResultStatus = AllocationStatusUnresolved;
                ScheduleWalletBatchReconciliation(batch, allocations, "PROVIDER_RETRY_UNRESOLVED", "Wallet provider retry did not return final statuses for all allocations.");
                await MarkReconciliationUnknownAsync(batch, allocations, "PROVIDER_RETRY_UNRESOLVED", "Wallet provider retry did not return final statuses for all allocations.");
            }
            else
            {
                attempt.ResultStatus = ReconciliationStatusResolved;
            }
        }
        catch (Exception ex)
        {
            attempt.ErrorMessage = ex.Message;
            attempt.CompletedAt = DateTime.UtcNow;
            attempt.ResultStatus = AllocationStatusUnresolved;
            await MarkReconciliationUnknownAsync(batch, allocations, "PROVIDER_RETRY_EXCEPTION", ex.Message);
        }

        foreach (var entry in batch.SalaryCycle.Entries)
            RefreshEntryStatusFromAllocations(entry);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task PostWalletBatchAsync(
        SalaryCycle cycle,
        string channel,
        List<SalaryEntryAllocation> allocations,
        string coreReferenceId,
        int postedBy)
    {
        if (allocations.Count == 0)
            return;

        var shadowAccount = WalletShadowAccount(channel);
        var batchReference = $"SAL-{cycle.Id}-{channel.ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        var request = BuildWalletTransferRequest(cycle, channel, batchReference, coreReferenceId, allocations);

        var batch = new SalaryWalletBatch
        {
            SalaryCycleId = cycle.Id,
            SalaryCycle = cycle,
            PostedByUserId = postedBy,
            WalletChannel = channel,
            ShadowAccount = shadowAccount,
            BatchReference = batchReference,
            CoreReferenceId = coreReferenceId,
            RequestedTotalAmount = request.RequestedTotalAmount,
            OverallStatus = "pending",
            ReversalStatus = "not_required",
            ReconciliationStatus = ReconciliationStatusNotRequired,
            ReconciliationMode = ReconciliationModeFor(channel),
            MaxAttempts = MaxAttemptsFor(channel),
            ProviderRequestJson = JsonSerializer.Serialize(request, ProviderJsonOptions)
        };
        cycle.WalletBatches.Add(batch);

        WalletSalaryTransferResponseDto response;
        try
        {
            response = await _walletProvider.PostSalaryWalletBatchAsync(request);
        }
        catch (Exception ex)
        {
            foreach (var allocation in allocations)
            {
                allocation.Status = AllocationStatusUnresolved;
                allocation.TransferResultCode = "PROVIDER_NO_RESPONSE";
                allocation.TransferResultReason = "Wallet provider did not return a usable response.";
            }
            ScheduleWalletBatchReconciliation(batch, allocations, "PROVIDER_NO_RESPONSE", ex.Message);
            return;
        }

        var stillUnresolved = await ApplyWalletProviderResponseAsync(
            batch,
            allocations,
            response,
            postedBy,
            reverseWhenFinal: true);

        if (stillUnresolved)
            ScheduleWalletBatchReconciliation(batch, allocations, "PROVIDER_RESULT_UNRESOLVED", "Wallet provider response did not return final statuses for all allocations.");
    }

    private async Task MarkWalletFundingFailureAsync(
        SalaryCycle cycle,
        string channel,
        List<SalaryEntryAllocation> allocations,
        decimal requestedTotal,
        bool shadowFunded,
        string coreReferenceId)
    {
        if (allocations.Count == 0)
            return;

        var batch = new SalaryWalletBatch
        {
            SalaryCycleId = cycle.Id,
            SalaryCycle = cycle,
            WalletChannel = channel,
            ShadowAccount = WalletShadowAccount(channel),
            BatchReference = $"SAL-{cycle.Id}-{channel.ToUpperInvariant()}-FUNDING-FAILED-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}",
            CoreReferenceId = coreReferenceId,
            RequestedTotalAmount = requestedTotal,
            FailedTotalAmount = requestedTotal,
            OverallStatus = AllocationStatusFailed,
            ProviderErrorMessage = "Required wallet shadow account funding was not fully confirmed. Provider call was skipped.",
            ReversalStatus = "not_required"
        };

        cycle.WalletBatches.Add(batch);

        foreach (var allocation in allocations)
        {
            allocation.Status = AllocationStatusFailed;
            allocation.TransferResultCode = "SHADOW_FUNDING_FAILED";
            allocation.TransferResultReason = "Required wallet shadow account funding was not confirmed. Provider call was skipped.";
        }

        if (shadowFunded)
            await ReverseWalletAmountAsync(batch, cycle, batch.ShadowAccount, requestedTotal, $"Wallet funding reversal {channel.ToUpperInvariant()}");
    }

    public async Task<PagedResult<SalaryCycleDto>> GetSalaryCyclesAsync(int companyId, int page, int limit)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 50;
        if (limit > 100) limit = 100;

        var query = _db.SalaryCycles
            .Where(s => s.CompanyId == companyId)
            .AsNoTracking();

        var total = await query.CountAsync();
        var list = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(s => new SalaryCycleDto
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
                EntryCount = s.Entries.Count()
            })
            .ToListAsync();

        return new PagedResult<SalaryCycleDto>
        {
            Page = page,
            Limit = limit,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)limit),
            Data = list
        };
    }

    public async Task<SalaryCycleDto> CreateSalaryCycleAsync(int companyId, int createdByUserId, SalaryCycleCreateDto dto)
    {
        List<(Employee emp, decimal amount, SalaryEntryUpsertDto? dto)> list;

        /* a) caller sent an explicit list ------------------------------- */
        if (dto.Entries is { Count: > 0 })
        {
            // fetch only those employees and keep the order from dto
            var empIds = dto.Entries.Select(e => e.EmployeeId).ToList();
            var requestedEmployeeIds = empIds.Distinct().ToList();
            var empMap = await _db.Employees
                                 .Where(e => e.CompanyId == companyId &&
                                             !e.IsDeleted &&
                                             requestedEmployeeIds.Contains(e.Id))
                                 .ToDictionaryAsync(e => e.Id);

            var missingEmployeeIds = requestedEmployeeIds
                .Where(id => !empMap.ContainsKey(id))
                .OrderBy(x => x)
                .ToList();
            if (missingEmployeeIds.Count > 0)
                throw new InvalidOperationException($"Employee(s) not found or deleted for this company: {string.Join(", ", missingEmployeeIds)}.");

            list = dto.Entries
                     .Select(e => (empMap[e.EmployeeId], e.Salary, (SalaryEntryUpsertDto?)e))
                     .ToList();
        }
        /* b) legacy behaviour ------------------------------------------ */
        else
        {
            var employees = await _db.Employees
                 .Where(e => e.CompanyId == companyId && e.SendSalary && !e.IsDeleted)
                 .ToListAsync();

            list = employees.Select(e => (e, e.Salary, (SalaryEntryUpsertDto?)null)).ToList();
        }

        // Round per-currency: LYD -> 3dp, others -> 2dp
        var decimals = string.Equals(dto.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
        list = list.Select(x => (emp: x.emp, amount: Math.Round(x.amount, decimals), dto: x.dto)).ToList();
        var total = Math.Round(list.Sum(x => x.amount), decimals);
        // normalize additionalMonth: only keep if between 13 and 24 (inclusive)
        string? additionalMonthStr = null;
        if (dto.AdditionalMonth.HasValue && dto.AdditionalMonth.Value >= 13 && dto.AdditionalMonth.Value <= 24)
            additionalMonthStr = dto.AdditionalMonth.Value.ToString();

        await using var tx = await _db.Database.BeginTransactionAsync();

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
                Employee = x.emp,
                Amount = Math.Round(x.amount, decimals)
            }).ToList()
        };

        _db.SalaryCycles.Add(cycle);
        await _db.SaveChangesAsync();

        foreach (var entry in cycle.Entries)
        {
            var source = list.First(x => x.emp.Id == entry.EmployeeId);
            var allocations = BuildAllocationsForEntry(entry, source.emp, source.dto, cycle.Currency, throwIfNoDefault: false);
            foreach (var allocation in allocations)
                entry.Allocations.Add(allocation);
        }
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        return _mapper.Map<SalaryCycleDto>(cycle);
    }

    private async Task<SalaryCycleDto?> PostSalaryCycleWithWalletsAsync(int companyId, int cycleId, int postedBy)
    {
        var cycle = await _db.SalaryCycles
            .Include(c => c.Entries).ThenInclude(e => e.Employee)
            .Include(c => c.Entries).ThenInclude(e => e.Allocations)
            .Include(c => c.WalletBatches)
            .Include(c => c.Company).ThenInclude(c => c.ServicePackage)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.CompanyId == companyId);

        if (cycle is null) return null;
        if (cycle.PostedAt != null) return null;

        var pkgId = cycle.Company.ServicePackageId;
        var detail = await _db.ServicePackageDetails
            .Include(d => d.TransactionCategory)
            .FirstOrDefaultAsync(d => d.ServicePackageId == pkgId &&
                                      d.TransactionCategory.Name == "Salary Payment");
        if (detail is null || !detail.IsEnabledForPackage)
            throw SalaryError(
                "SALARY_SERVICE_PACKAGE_DISABLED",
                "Salary payments are not enabled for your service package.",
                "خدمة دفع الرواتب غير مفعلة في باقة الخدمة.",
                new
                {
                    salaryCycleId = cycle.Id,
                    servicePackageId = pkgId
                },
                403);

        var debitAcc13 = NormalizeAcc13(cycle.DebitAccount);
        if (debitAcc13.Length != 13)
            throw SalaryError(
                "SALARY_DEBIT_ACCOUNT_INVALID",
                "Debit account must be 13 digits.",
                "يجب أن يكون حساب الخصم مكونا من 13 رقما.",
                new
                {
                    salaryCycleId = cycle.Id,
                    debitAccount = cycle.DebitAccount
                });

        await EnsureAllocationsForCycleAsync(cycle);

        var allAllocations = cycle.Entries
            .Where(e => e.Employee != null && !e.Employee.IsDeleted)
            .SelectMany(e => e.Allocations)
            .Where(a => a.Amount > 0m)
            .ToList();

        if (allAllocations.Count == 0)
            throw SalaryError(
                "SALARY_NO_ELIGIBLE_ALLOCATIONS",
                "No eligible salary allocations were found.",
                "لا توجد توزيعات رواتب مؤهلة للنشر.",
                new
                {
                    salaryCycleId = cycle.Id
                });

        foreach (var allocation in allAllocations)
            ResetAllocationForPosting(allocation);

        var accountAllocations = allAllocations
            .Where(a => string.Equals(a.PaymentChannel, PaymentChannelAccount, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var evoAllocations = allAllocations
            .Where(a => string.Equals(a.PaymentChannel, PaymentChannelEvo, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var bcdAllocations = allAllocations
            .Where(a => string.Equals(a.PaymentChannel, PaymentChannelBcd, StringComparison.OrdinalIgnoreCase))
            .ToList();

        const int trxCatSalaryFixedFee = 17;
        decimal fixedFee = 0m;
        string? feeGl = null;
        string feeNarration = "Salary receiver-paid fixed fee";

        if (accountAllocations.Count > 0)
        {
            var feePricing = await _db.Pricings.AsNoTracking()
                .FirstOrDefaultAsync(p => p.TrxCatId == trxCatSalaryFixedFee && p.Unit == 1);
            if (feePricing == null)
                throw SalaryError(
                    "SALARY_PRICING_NOT_CONFIGURED",
                    "Pricing for salary fixed fee is not configured.",
                    "تسعيرة عمولة الراتب غير مهيأة.",
                    new
                    {
                        salaryCycleId = cycle.Id,
                        trxCatId = trxCatSalaryFixedFee,
                        unit = 1
                    },
                    500);

            fixedFee = feePricing.Price ?? 0m;
            if (fixedFee <= 0m)
                throw SalaryError(
                    "SALARY_FIXED_FEE_INVALID",
                    "Invalid pricing: fixed fee must be greater than zero.",
                    "عمولة الراتب الثابتة غير صالحة.",
                    new
                    {
                        salaryCycleId = cycle.Id,
                        fixedFee
                    },
                    500);

            feeGl = !string.IsNullOrWhiteSpace(feePricing.GL1)
                ? NormalizeAcc13(feePricing.GL1)
                : BuildCommissionGlFromSender(debitAcc13);
            if (feeGl.Length != 13)
                throw SalaryError(
                    "SALARY_FEE_GL_INVALID",
                    "Configured fee GL must be a 13-digit account.",
                    "حساب عمولة الراتب غير صالح.",
                    new
                    {
                        salaryCycleId = cycle.Id,
                        feeGl
                    },
                    500);

            feeNarration = string.IsNullOrWhiteSpace(feePricing.NR2)
                ? feeNarration
                : feePricing.NR2;

            foreach (var allocation in accountAllocations)
            {
                if (allocation.Amount < fixedFee)
                    throw SalaryError(
                        "SALARY_ALLOCATION_LESS_THAN_FEE",
                        "Salary allocation amount is less than the fixed fee.",
                        "مبلغ توزيع الراتب أقل من العمولة الثابتة.",
                        new
                        {
                            salaryCycleId = cycle.Id,
                            salaryEntryId = allocation.SalaryEntryId,
                            employeeId = allocation.SalaryEntry.EmployeeId,
                            amount = allocation.Amount,
                            fixedFee,
                            currency = cycle.Currency
                        });
            }
        }

        var salaryMonthText = cycle.SalaryMonth ?? string.Empty;
        var customerNumber = debitAcc13.Substring(4, 6);
        var baseId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var hidFunding = baseId + "BA00";
        var journalEntries = new List<Dictionary<string, string>>();
        var expectedCreditAccounts = new List<string>();
        var lineNo = 1;

        foreach (var allocation in accountAllocations)
        {
            var destination = NormalizeAcc13(allocation.Destination);
            journalEntries.Add(new Dictionary<string, string>
            {
                ["YBCD10DID"] = baseId + $"B{lineNo:00}",
                ["YBCD10ACC"] = destination,
                ["YBCD10AMT"] = ToCoreAmount(allocation.Amount, cycle.Currency),
                ["YBCD10CCY"] = cycle.Currency,
                ["YBCD10NR1"] = $"Salary {salaryMonthText}",
                ["YBCD10NR2"] = string.Empty,
                ["YBCD10NR3"] = string.Empty,
                ["YBCD10NR4"] = string.Empty
            });
            expectedCreditAccounts.Add(destination);
            lineNo++;
        }

        var evoTotal = Math.Round(evoAllocations.Sum(a => a.Amount), CurrencyDecimals(cycle.Currency));
        var bcdTotal = Math.Round(bcdAllocations.Sum(a => a.Amount), CurrencyDecimals(cycle.Currency));

        if (evoTotal > 0m)
        {
            journalEntries.Add(new Dictionary<string, string>
            {
                ["YBCD10DID"] = baseId + $"B{lineNo:00}",
                ["YBCD10ACC"] = EvoShadowAccount,
                ["YBCD10AMT"] = ToCoreAmount(evoTotal, cycle.Currency),
                ["YBCD10CCY"] = cycle.Currency,
                ["YBCD10NR1"] = $"Salary wallet funding EVO {salaryMonthText}",
                ["YBCD10NR2"] = string.Empty,
                ["YBCD10NR3"] = string.Empty,
                ["YBCD10NR4"] = string.Empty
            });
            expectedCreditAccounts.Add(EvoShadowAccount);
            lineNo++;
        }

        if (bcdTotal > 0m)
        {
            journalEntries.Add(new Dictionary<string, string>
            {
                ["YBCD10DID"] = baseId + $"B{lineNo:00}",
                ["YBCD10ACC"] = BcdShadowAccount,
                ["YBCD10AMT"] = ToCoreAmount(bcdTotal, cycle.Currency),
                ["YBCD10CCY"] = cycle.Currency,
                ["YBCD10NR1"] = $"Salary wallet funding BCD {salaryMonthText}",
                ["YBCD10NR2"] = string.Empty,
                ["YBCD10NR3"] = string.Empty,
                ["YBCD10NR4"] = string.Empty
            });
            expectedCreditAccounts.Add(BcdShadowAccount);
        }

        var fundingPayload = BuildCoreBatchPayload(
            hidFunding,
            "D",
            debitAcc13,
            cycle.Currency,
            customerNumber,
            $"Salary {salaryMonthText}",
            journalEntries);

        cycle.BankReference = hidFunding;
        await _db.SaveChangesAsync();

        var fundingResult = await PostCoreBatchAsync(fundingPayload, expectedCreditAccounts);
        cycle.BankResponseRaw = fundingResult.Raw;
        await _db.SaveChangesAsync();

        if (!fundingResult.IsHttpSuccess)
            throw SalaryError(
                "SALARY_BANK_FUNDING_FAILED",
                "Bank salary funding request failed.",
                "فشل تمويل دفعة الرواتب من النظام البنكي.",
                new
                {
                    salaryCycleId = cycle.Id,
                    bankReference = hidFunding,
                    bankStatus = fundingResult.StatusCode,
                    bankResponse = fundingResult.Raw
                },
                502);

        var successfulAccountAllocations = new List<SalaryEntryAllocation>();

        foreach (var allocation in accountAllocations)
        {
            var destination = NormalizeAcc13(allocation.Destination);
            fundingResult.Outcomes.TryGetValue(destination, out var outcome);
            allocation.RawResponse = outcome?.Raw;

            if (fundingResult.SuccessAccounts.Contains(destination))
            {
                allocation.Status = AllocationStatusSuccess;
                allocation.TransferResultCode = "S";
                allocation.IsTransferred = true;
                allocation.TransferredAt = DateTime.UtcNow;
                allocation.PostedByUserId = postedBy;
                successfulAccountAllocations.Add(allocation);
            }
            else
            {
                allocation.Status = AllocationStatusFailed;
                allocation.TransferResultCode = outcome?.Code;
                allocation.TransferResultReason = outcome?.Reason ?? "Core salary funding did not confirm this account allocation.";
            }
        }

        var evoShadowFunded = evoTotal == 0m || fundingResult.SuccessAccounts.Contains(EvoShadowAccount);
        var bcdShadowFunded = bcdTotal == 0m || fundingResult.SuccessAccounts.Contains(BcdShadowAccount);
        var shadowFundingReady = evoShadowFunded && bcdShadowFunded;

        if (expectedCreditAccounts.Count > 0 && fundingResult.SuccessAccounts.Count == 0)
        {
            var hasInsufficientBalance = string.Equals(fundingResult.ReturnMessageCode, "RC00000210", StringComparison.OrdinalIgnoreCase) ||
                (fundingResult.ReturnMessage?.Contains("No Available Balance", StringComparison.OrdinalIgnoreCase) ?? false);
            var coreFailureCode = hasInsufficientBalance
                ? "SALARY_INSUFFICIENT_DEBIT_BALANCE"
                : "SALARY_CORE_FUNDING_FAILED";
            var coreFailureMessageEn = hasInsufficientBalance
                ? "Insufficient debit account balance."
                : "Core salary funding failed.";
            var coreFailureMessageAr = hasInsufficientBalance
                ? "رصيد حساب الخصم غير كاف."
                : "فشل تمويل الرواتب من النظام البنكي.";
            var allocationFailureReason = string.IsNullOrWhiteSpace(fundingResult.ReturnMessage)
                ? coreFailureMessageEn
                : fundingResult.ReturnMessage!;
            var persistedFailureCode = string.IsNullOrWhiteSpace(fundingResult.ReturnMessageCode)
                ? "CORE_FUNDING_FAILED"
                : fundingResult.ReturnMessageCode!.Trim();
            if (persistedFailureCode.Length > 32)
                persistedFailureCode = persistedFailureCode[..32];
            var persistedFailureReason = allocationFailureReason.Length > 1024
                ? allocationFailureReason[..1024]
                : allocationFailureReason;

            foreach (var allocation in allAllocations)
            {
                allocation.Status = AllocationStatusFailed;
                allocation.TransferResultCode = persistedFailureCode;
                allocation.TransferResultReason = persistedFailureReason;
            }

            foreach (var entry in cycle.Entries)
                RefreshEntryStatusFromAllocations(entry);

            await _db.SaveChangesAsync();

            var failedAllocations = allAllocations
                .Select(a => new
                {
                    salaryEntryId = a.SalaryEntryId,
                    employeeId = a.SalaryEntry.EmployeeId,
                    paymentChannel = a.PaymentChannel,
                    amount = a.Amount,
                    destination = a.Destination,
                    transferResultCode = string.IsNullOrWhiteSpace(a.TransferResultCode)
                        ? persistedFailureCode
                        : a.TransferResultCode,
                    transferResultReason = string.IsNullOrWhiteSpace(a.TransferResultReason)
                        ? persistedFailureReason
                        : a.TransferResultReason
                })
                .ToList();

            throw SalaryError(
                coreFailureCode,
                coreFailureMessageEn,
                coreFailureMessageAr,
                new
                {
                    salaryCycleId = cycle.Id,
                    bankReference = hidFunding,
                    debitAccount = debitAcc13,
                    requestedAmount = Math.Round(
                        accountAllocations.Sum(a => a.Amount) + evoTotal + bcdTotal,
                        CurrencyDecimals(cycle.Currency)),
                    currency = cycle.Currency,
                    coreReturnCode = fundingResult.ReturnCode,
                    coreReturnMessageCode = fundingResult.ReturnMessageCode,
                    coreReturnMessage = fundingResult.ReturnMessage,
                    failedAllocations
                },
                hasInsufficientBalance ? 400 : 502);
        }

        if (shadowFundingReady && successfulAccountAllocations.Count > 0 && feeGl != null)
        {
            var baseIdFee = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var hidFee = baseIdFee + "BF00";
            var feeEntries = new List<Dictionary<string, string>>();
            var feeLineNo = 1;

            foreach (var allocation in successfulAccountAllocations)
            {
                var destination = NormalizeAcc13(allocation.Destination);
                feeEntries.Add(new Dictionary<string, string>
                {
                    ["YBCD10DID"] = baseIdFee + $"B{feeLineNo:00}",
                    ["YBCD10ACC"] = destination,
                    ["YBCD10AMT"] = ToCoreAmount(fixedFee, cycle.Currency),
                    ["YBCD10CCY"] = cycle.Currency,
                    ["YBCD10NR1"] = $"Salary commission {salaryMonthText}",
                    ["YBCD10NR2"] = string.Empty,
                    ["YBCD10NR3"] = string.Empty,
                    ["YBCD10NR4"] = string.Empty
                });
                feeLineNo++;
                allocation.CommissionAmount = fixedFee;
            }

            var feePayload = BuildCoreBatchPayload(
                hidFee,
                "C",
                feeGl,
                cycle.Currency,
                customerNumber,
                feeNarration,
                feeEntries);

            var feeResult = await PostCoreBatchAsync(feePayload, successfulAccountAllocations.Select(a => a.Destination));
            cycle.BankFeeReference = hidFee;
            cycle.BankFeeResponseRaw = feeResult.Raw;
        }

        if (!shadowFundingReady)
        {
            await ReverseSuccessfulBankAllocationsAsync(cycle, successfulAccountAllocations, debitAcc13, customerNumber, salaryMonthText);
            await MarkWalletFundingFailureAsync(cycle, PaymentChannelEvo, evoAllocations, evoTotal, evoShadowFunded, hidFunding);
            await MarkWalletFundingFailureAsync(cycle, PaymentChannelBcd, bcdAllocations, bcdTotal, bcdShadowFunded, hidFunding);

            foreach (var entry in cycle.Entries)
                RefreshEntryStatusFromAllocations(entry);

            await _db.SaveChangesAsync();

            var failedShadowAccounts = new List<object>();
            if (evoTotal > 0m && !evoShadowFunded)
                failedShadowAccounts.Add(new
                {
                    walletChannel = PaymentChannelEvo,
                    shadowAccount = EvoShadowAccount,
                    requestedAmount = evoTotal
                });
            if (bcdTotal > 0m && !bcdShadowFunded)
                failedShadowAccounts.Add(new
                {
                    walletChannel = PaymentChannelBcd,
                    shadowAccount = BcdShadowAccount,
                    requestedAmount = bcdTotal
                });

            throw SalaryError(
                "SALARY_SHADOW_FUNDING_FAILED",
                "Wallet shadow account funding failed.",
                "فشل تمويل حسابات المحافظ الوسيطة.",
                new
                {
                    salaryCycleId = cycle.Id,
                    bankReference = hidFunding,
                    failedShadowAccounts
                },
                502);
        }
        else
        {
            await PostWalletBatchAsync(cycle, PaymentChannelEvo, evoAllocations, hidFunding, postedBy);
            await PostWalletBatchAsync(cycle, PaymentChannelBcd, bcdAllocations, hidFunding, postedBy);
        }

        foreach (var entry in cycle.Entries)
            RefreshEntryStatusFromAllocations(entry);

        var successTotal = allAllocations
            .Where(a => a.IsTransferred)
            .Sum(a => a.Amount);

        if (successTotal > 0m)
        {
            cycle.TotalAmount = Math.Round(successTotal, CurrencyDecimals(cycle.Currency));
            cycle.PostedAt = DateTime.UtcNow;
            cycle.PostedByUserId = postedBy;
        }

        await _db.SaveChangesAsync();
        return _mapper.Map<SalaryCycleDto>(cycle);
    }

    public async Task<SalaryCycleDto?> PostSalaryCycleAsync(int companyId, int cycleId, int postedBy)
    {
        return await PostSalaryCycleWithWalletsAsync(companyId, cycleId, postedBy);
#if false
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
                        && !e.Employee.IsDeleted
                        && e.Employee.AccountType.Equals("account", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(e.Employee.AccountNumber)
                        && e.Employee.AccountNumber!.Length == 13)
            .ToList();
        if (eligibleEntries.Count == 0)
            throw new PayrollException("No eligible active employees (13-digit account numbers of type 'account') found.");

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
#endif
    }


    public async Task<EmployeeDto?> GetEmployeeAsync(int companyId, int employeeId)
    {
        var e = await _db.Employees
                         .AsNoTracking()
                         .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == employeeId && !x.IsDeleted);

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
                EvoWallet = e.EvoWallet,
                BcdWallet = e.BcdWallet,
                AccountAllocationAmount = e.AccountAllocationAmount,
                EvoAllocationAmount = e.EvoAllocationAmount,
                BcdAllocationAmount = e.BcdAllocationAmount,
                SendSalary = e.SendSalary,
                CanPost = e.CanPost,
                IsDeleted = e.IsDeleted
            };
    }

    public async Task<SalaryCycleDto?> GetSalaryCycleAsync(int companyId, int cycleId)
    {
        var cycle = await _db.SalaryCycles
            .AsNoTracking()
            .Include(c => c.Entries).ThenInclude(e => e.Employee)
            .Include(c => c.Entries).ThenInclude(e => e.Allocations)
            .Include(c => c.WalletBatches)
            .Where(c => c.CompanyId == companyId && c.Id == cycleId)
            .FirstOrDefaultAsync();

        return cycle == null ? null : _mapper.Map<SalaryCycleDto>(cycle);
    }

    public async Task<SalaryEntryDto?> GetSalaryEntryAsync(int companyId, int cycleId, int entryId)
    {
        var en = await _db.SalaryEntries
                          .Include(e => e.Employee)
                          .Include(e => e.Allocations)
                          .Include(e => e.SalaryCycle)
                          .AsNoTracking()
                          .FirstOrDefaultAsync(e =>
                              e.Id == entryId &&
                              e.SalaryCycleId == cycleId &&
                              e.SalaryCycle.CompanyId == companyId);

        return en == null ? null : _mapper.Map<SalaryEntryDto>(en);
    }

    public async Task<List<SalaryEntryDto>> GetFailedEntriesAsync(int companyId, int cycleId)
    {
        var list = await _db.SalaryEntries
            .Include(e => e.Employee)
            .Include(e => e.Allocations)
            .Include(e => e.SalaryCycle)
            .Where(e => e.SalaryCycle.CompanyId == companyId && e.SalaryCycleId == cycleId && !e.IsTransferred && !e.Employee.IsDeleted)
            .AsNoTracking()
            .ToListAsync();

        return list.Select(en => _mapper.Map<SalaryEntryDto>(en)).ToList();
    }

    public async Task<SalaryEntryDto?> EditEntryAndEmployeeAsync(int companyId, int cycleId, int entryId, SalaryEntryEditDto dto)
    {
        var en = await _db.SalaryEntries
            .Include(e => e.Employee)
            .Include(e => e.Allocations)
            .Include(e => e.SalaryCycle)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.SalaryCycleId == cycleId && e.SalaryCycle.CompanyId == companyId);

        if (en == null) return null;
        if (en.IsTransferred) throw new PayrollException("Cannot edit an entry that has already been transferred.");

        if (dto.Amount.HasValue)
        {
            var cycle = await _db.SalaryCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == en.SalaryCycleId);
            var decimals = (cycle != null && string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase)) ? 3 : 2;
            en.Amount = Math.Round(dto.Amount.Value, decimals);
            if (en.Allocations.Count == 1)
                en.Allocations.First().Amount = en.Amount;
        }
        if (!string.IsNullOrWhiteSpace(dto.AccountNumber)) en.Employee.AccountNumber = dto.AccountNumber!;
        if (!string.IsNullOrWhiteSpace(dto.AccountType)) en.Employee.AccountType = dto.AccountType!;
        if (!string.IsNullOrWhiteSpace(dto.EvoWallet)) en.Employee.EvoWallet = dto.EvoWallet!;
        if (!string.IsNullOrWhiteSpace(dto.BcdWallet)) en.Employee.BcdWallet = dto.BcdWallet!;

        foreach (var allocation in en.Allocations)
            allocation.Destination = ResolveAllocationDestination(en.Employee, NormalizePaymentChannel(allocation.PaymentChannel), allocation.Destination);

        await _db.SaveChangesAsync();

        return _mapper.Map<SalaryEntryDto>(en);
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
                        e.Employee != null &&
                        !e.Employee.IsDeleted &&
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
            .Include(c => c.Entries).ThenInclude(e => e.Allocations)
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.CompanyId == companyId);

        if (cycle == null || cycle.PostedAt != null)
            return null;

        if (dto.SalaryMonth is not null) cycle.SalaryMonth = dto.SalaryMonth;
        if (dto.AdditionalMonth is not null) cycle.AdditionalMonth = dto.AdditionalMonth.Value.ToString();
        if (dto.DebitAccount is not null) cycle.DebitAccount = dto.DebitAccount;
        if (dto.Currency is not null) cycle.Currency = dto.Currency;

        var incoming = (dto.Entries ?? Enumerable.Empty<SalaryEntryUpsertDto>())
                       .ToDictionary(e => e.EmployeeId, e => e);

        if (incoming.Count > 0)
        {
            var requestedEmployeeIds = incoming.Keys.ToList();
            var activeEmployeeIds = await _db.Employees
                .Where(e => e.CompanyId == companyId && !e.IsDeleted && requestedEmployeeIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync();
            var activeEmployeeSet = activeEmployeeIds.ToHashSet();
            var missingEmployeeIds = requestedEmployeeIds
                .Where(id => !activeEmployeeSet.Contains(id))
                .OrderBy(x => x)
                .ToList();
            if (missingEmployeeIds.Count > 0)
                throw new InvalidOperationException($"Employee(s) not found or deleted for this company: {string.Join(", ", missingEmployeeIds)}.");
        }

        foreach (var existing in cycle.Entries.ToList())
        {
            if (incoming.TryGetValue(existing.EmployeeId, out var inc))
            {
                // Round per cycle currency precision (LYD=3dp, others=2dp)
                var decimals = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
                existing.Amount = Math.Round(inc.Salary, decimals);
                _db.SalaryEntryAllocations.RemoveRange(existing.Allocations);
                existing.Allocations.Clear();
                foreach (var allocation in BuildAllocationsForEntry(existing, existing.Employee, inc, cycle.Currency, throwIfNoDefault: false))
                    existing.Allocations.Add(allocation);
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
                                   e.CompanyId == companyId &&
                                   !e.IsDeleted);
            if (emp == null) continue;

            var decimals = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
            cycle.Entries.Add(new SalaryEntry
            {
                EmployeeId = emp.Id,
                Employee = emp,
                Amount = Math.Round(inc.Salary, decimals),
                Allocations = new List<SalaryEntryAllocation>()
            });
        }

        await _db.SaveChangesAsync();

        foreach (var entry in cycle.Entries.Where(e => e.Allocations.Count == 0).ToList())
        {
            var inc = (dto.Entries ?? new List<SalaryEntryUpsertDto>()).FirstOrDefault(x => x.EmployeeId == entry.EmployeeId);
            if (inc == null || entry.Employee == null)
                continue;

            foreach (var allocation in BuildAllocationsForEntry(entry, entry.Employee, inc, cycle.Currency, throwIfNoDefault: false))
                entry.Allocations.Add(allocation);
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
            .Include(c => c.Entries).ThenInclude(e => e.Allocations)
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
            .Where(e => e.CompanyId == companyId && !e.IsDeleted && requestedEmployeeIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync();

        var companyEmployeeSet = companyEmployeeIds.ToHashSet();
        var missingEmployeeIds = requestedEmployeeIds
            .Where(id => !companyEmployeeSet.Contains(id))
            .OrderBy(x => x)
            .ToList();
        if (missingEmployeeIds.Count > 0)
            throw new PayrollException($"Employee(s) not found or deleted for this company: {string.Join(", ", missingEmployeeIds)}.");

        var decimals = string.Equals(cycle.Currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
        var newEntries = new List<SalaryEntry>();

        foreach (var inc in dto.Entries)
        {
            var emp = await _db.Employees
                .FirstOrDefaultAsync(e => e.Id == inc.EmployeeId && e.CompanyId == companyId && !e.IsDeleted);
            if (emp == null) continue;

            var entry = new SalaryEntry
            {
                EmployeeId = inc.EmployeeId,
                Employee = emp,
                Amount = Math.Round(inc.Salary, decimals),
                Allocations = new List<SalaryEntryAllocation>()
            };
            cycle.Entries.Add(entry);
            newEntries.Add(entry);
        }

        await _db.SaveChangesAsync();

        foreach (var entry in newEntries)
        {
            var inc = dto.Entries.FirstOrDefault(x => x.EmployeeId == entry.EmployeeId);
            if (inc == null || entry.Employee == null)
                continue;

            foreach (var allocation in BuildAllocationsForEntry(entry, entry.Employee, inc, cycle.Currency, throwIfNoDefault: false))
                entry.Allocations.Add(allocation);
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
            .Include(x => x.Entries).ThenInclude(e => e.Allocations)
            .Include(x => x.WalletBatches)
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
                EvoWallet = e.Employee.EvoWallet,
                BcdWallet = e.Employee.BcdWallet,
                SendSalary = e.Employee.SendSalary,
                CanPost = e.Employee.CanPost,
                IsDeleted = e.Employee.IsDeleted,
                IsTransferred = e.IsTransferred,
                TransferResultCode = e.TransferResultCode,
                TransferResultReason = e.TransferResultReason,
                TransferredAt = e.TransferredAt,
                CommissionAmount = e.CommissionAmount,
                Allocations = e.Allocations.Select(a => new SalaryEntryAllocationDto
                {
                    Id = a.Id,
                    SalaryEntryId = a.SalaryEntryId,
                    PaymentChannel = a.PaymentChannel,
                    Amount = a.Amount,
                    Destination = a.Destination,
                    ClientReference = a.ClientReference,
                    Status = a.Status,
                    TransferResultCode = a.TransferResultCode,
                    TransferResultReason = a.TransferResultReason,
                    ProviderTransactionId = a.ProviderTransactionId,
                    CommissionAmount = a.CommissionAmount,
                    IsTransferred = a.IsTransferred,
                    TransferredAt = a.TransferredAt
                }).ToList()
            }).ToList(),
            WalletBatches = s.WalletBatches.Select(b => new SalaryWalletBatchDto
            {
                Id = b.Id,
                SalaryCycleId = b.SalaryCycleId,
                WalletChannel = b.WalletChannel,
                ShadowAccount = b.ShadowAccount,
                BatchReference = b.BatchReference,
                CoreReferenceId = b.CoreReferenceId,
                RequestedTotalAmount = b.RequestedTotalAmount,
                SuccessfulTotalAmount = b.SuccessfulTotalAmount,
                FailedTotalAmount = b.FailedTotalAmount,
                TotalCommission = b.TotalCommission,
                OverallStatus = b.OverallStatus,
                ReversalStatus = b.ReversalStatus,
                ReversalAmount = b.ReversalAmount,
                ReversalBankReference = b.ReversalBankReference,
                ProcessedAt = b.ProcessedAt,
                ReversedAt = b.ReversedAt
            }).ToList()
        };
    }

    #endregion


}
