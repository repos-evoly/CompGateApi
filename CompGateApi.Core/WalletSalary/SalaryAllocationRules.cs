using CompGateApi.Core.Dtos;
using CompGateApi.Core.Errors;

namespace CompGateApi.Core.WalletSalary;

public static class SalaryAllocationRules
{
    public static string ValidateCompanyDebitAccount(string? account, string? companyCode)
    {
        var normalizedAccount = account?.Trim() ?? string.Empty;
        if (normalizedAccount.Length != 13 || !normalizedAccount.All(char.IsDigit))
            throw new PayrollException("Salary debit account must contain exactly 13 digits.");

        var normalizedCompanyCode = companyCode?.Trim() ?? string.Empty;
        if (normalizedCompanyCode.Length != 6 || !normalizedCompanyCode.All(char.IsDigit) ||
            !string.Equals(
                normalizedAccount.Substring(4, 6),
                normalizedCompanyCode,
                StringComparison.Ordinal))
        {
            throw new PayrollException("Salary debit account does not belong to this company.");
        }

        return normalizedAccount;
    }

    public static IReadOnlyList<SalaryEntryAllocation> BuildAllocationSnapshots(
        Employee employee,
        SalaryEntryUpsertDto entry,
        string currency)
    {
        var decimals = string.Equals(currency, "LYD", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
        var salary = Math.Round(entry.Salary, decimals, MidpointRounding.AwayFromZero);
        if (salary <= 0m)
            throw new PayrollException($"Salary must be greater than zero for employee #{employee.Id}.");

        var requested = entry.Allocations is { Count: > 0 }
            ? entry.Allocations
            : BuildEmployeeDefaultAllocations(employee, salary, decimals);

        var normalized = new List<(string Channel, decimal Amount)>();
        foreach (var allocation in requested)
        {
            var channel = WalletSalaryChannels.Normalize(allocation.PaymentChannel);
            if (channel is null)
                throw new PayrollException($"Unsupported salary payment channel '{allocation.PaymentChannel}' for employee #{employee.Id}.");

            var amount = Math.Round(allocation.Amount, decimals, MidpointRounding.AwayFromZero);
            if (amount <= 0m)
                throw new PayrollException($"Every salary allocation must be greater than zero for employee #{employee.Id}.");
            normalized.Add((channel, amount));
        }

        var duplicate = normalized
            .GroupBy(x => x.Channel, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new PayrollException($"Salary payment channel '{duplicate.Key}' can only appear once for employee #{employee.Id}.");

        var allocatedTotal = Math.Round(normalized.Sum(x => x.Amount), decimals, MidpointRounding.AwayFromZero);
        if (allocatedTotal != salary)
            throw new PayrollException(
                $"Salary allocations for employee #{employee.Id} must equal {salary:0.###} {currency}; received {allocatedTotal:0.###}.");

        return normalized.Select(x => CreateSnapshot(employee, x.Channel, x.Amount)).ToList();
    }

    private static List<SalaryAllocationUpsertDto> BuildEmployeeDefaultAllocations(
        Employee employee,
        decimal salary,
        int decimals)
    {
        var defaults = new List<SalaryAllocationUpsertDto>();
        AddPositiveDefault(defaults, WalletSalaryChannels.BankAccount, employee.BankAllocationAmount);
        AddPositiveDefault(defaults, WalletSalaryChannels.EvoWallet, employee.EvoAllocationAmount);
        AddPositiveDefault(defaults, WalletSalaryChannels.BcdWallet, employee.BcdAllocationAmount);

        var total = Math.Round(defaults.Sum(x => x.Amount), decimals, MidpointRounding.AwayFromZero);
        var destinationsExist = defaults.All(x => x.PaymentChannel switch
        {
            WalletSalaryChannels.BankAccount => NormalizeOptional(employee.AccountNumber) is not null,
            WalletSalaryChannels.EvoWallet => NormalizeOptional(employee.EvoWallet) is not null,
            WalletSalaryChannels.BcdWallet => NormalizeOptional(employee.BcdWallet) is not null,
            _ => false
        });

        if (defaults.Count > 0 && total == salary && destinationsExist)
            return defaults;

        // Preserve the pre-default-allocation contract for legacy clients and
        // employees whose stored defaults are empty or no longer match salary.
        return new List<SalaryAllocationUpsertDto>
        {
            new() { PaymentChannel = WalletSalaryChannels.BankAccount, Amount = salary }
        };
    }

    private static void AddPositiveDefault(
        ICollection<SalaryAllocationUpsertDto> allocations,
        string channel,
        decimal amount)
    {
        if (amount > 0m)
            allocations.Add(new SalaryAllocationUpsertDto { PaymentChannel = channel, Amount = amount });
    }

    public static void NormalizeAndValidateEmployee(EmployeeCreateDto dto)
    {
        dto.Phone = NormalizeOptional(dto.Phone);
        dto.AccountNumber = NormalizeOptional(dto.AccountNumber);

        if (dto.AccountNumber is not null && !IsAccountNumber(dto.AccountNumber))
            throw new PayrollException("Employee bank account must contain exactly 13 digits.");

        dto.BankAllocationAmount = NormalizeAllocationAmount(dto.BankAllocationAmount, "Bank");
        dto.EvoAllocationAmount = NormalizeAllocationAmount(dto.EvoAllocationAmount, "EVO");
        dto.BcdAllocationAmount = NormalizeAllocationAmount(dto.BcdAllocationAmount, "BCD");

        var evo = NormalizeWalletDestination(
            dto.EvoWallet,
            dto.EvoWalletRecipientType,
            dto.EvoAllocationAmount,
            dto.Phone,
            "EVO");
        dto.EvoWallet = evo.Destination;
        dto.EvoWalletRecipientType = evo.RecipientType;

        var bcd = NormalizeWalletDestination(
            dto.BcdWallet,
            dto.BcdWalletRecipientType,
            dto.BcdAllocationAmount,
            dto.Phone,
            "BCD");
        dto.BcdWallet = bcd.Destination;
        dto.BcdWalletRecipientType = bcd.RecipientType;

        if (evo.CanonicalEmployeePhone is not null || bcd.CanonicalEmployeePhone is not null)
            dto.Phone = evo.CanonicalEmployeePhone ?? bcd.CanonicalEmployeePhone;

        if (dto.AccountNumber is null && dto.BankAllocationAmount != 0m)
            throw new PayrollException("Bank allocation must be zero when the employee has no bank account.");
        if (dto.EvoWallet is null && dto.EvoAllocationAmount != 0m)
            throw new PayrollException("EVO allocation must be zero when the employee has no EVO wallet destination.");
        if (dto.BcdWallet is null && dto.BcdAllocationAmount != 0m)
            throw new PayrollException("BCD allocation must be zero when the employee has no BCD wallet destination.");

        if (dto.SendSalary && dto.AccountNumber is null && dto.EvoWallet is null && dto.BcdWallet is null)
            throw new PayrollException("At least one employee salary destination is required.");

        if (dto.SendSalary)
        {
            var salary = Math.Round(dto.Salary, 3, MidpointRounding.AwayFromZero);
            if (salary != dto.Salary)
                throw new PayrollException("Employee salary cannot have more than three decimal places.");
            if (salary <= 0m)
                throw new PayrollException("Employee salary must be greater than zero when salary payment is enabled.");

            var allocatedTotal = dto.BankAllocationAmount +
                                 dto.EvoAllocationAmount +
                                 dto.BcdAllocationAmount;
            if (allocatedTotal != salary)
            {
                throw new PayrollException(
                    $"Employee default salary allocations must equal {salary:0.###} LYD; received {allocatedTotal:0.###} LYD.");
            }
        }
    }

    private static decimal NormalizeAllocationAmount(decimal amount, string label)
    {
        if (amount < 0m)
            throw new PayrollException($"{label} allocation cannot be negative.");

        var normalized = Math.Round(amount, 3, MidpointRounding.AwayFromZero);
        if (normalized != amount)
            throw new PayrollException($"{label} allocation cannot have more than three decimal places.");

        return normalized;
    }

    private static SalaryEntryAllocation CreateSnapshot(Employee employee, string channel, decimal amount)
    {
        var (destination, recipientType) = channel switch
        {
            WalletSalaryChannels.BankAccount =>
                (NormalizeOptional(employee.AccountNumber), WalletSalaryRecipientTypes.AccountNumber),
            WalletSalaryChannels.EvoWallet =>
                (NormalizeOptional(employee.EvoWallet), WalletSalaryRecipientTypes.NormalizeWalletType(employee.EvoWalletRecipientType)),
            WalletSalaryChannels.BcdWallet =>
                (NormalizeOptional(employee.BcdWallet), WalletSalaryRecipientTypes.NormalizeWalletType(employee.BcdWalletRecipientType)),
            _ => (null, null)
        };

        if (destination is null)
            throw new PayrollException($"Employee #{employee.Id} does not have a destination for {channel}.");
        if (destination.Length > 34)
            throw new PayrollException($"Employee #{employee.Id} destination for {channel} cannot exceed 34 characters.");
        if (channel == WalletSalaryChannels.BankAccount && !IsAccountNumber(destination))
            throw new PayrollException($"Employee #{employee.Id} bank account must contain exactly 13 digits.");
        if (channel != WalletSalaryChannels.BankAccount && recipientType is null)
            throw new PayrollException($"Employee #{employee.Id} must specify evoId or phoneNumber for {channel}.");
        if (recipientType == WalletSalaryRecipientTypes.PhoneNumber)
        {
            if (!LibyanPhoneNumber.TryNormalize(employee.Phone, out var employeePhone))
                throw new PayrollException($"Employee #{employee.Id} requires a valid Libyan employee phone number for {channel}.");
            if (!LibyanPhoneNumber.TryNormalize(destination, out var walletPhone) ||
                !string.Equals(walletPhone, employeePhone, StringComparison.Ordinal))
            {
                throw new PayrollException(
                    $"Employee #{employee.Id} wallet phone number for {channel} must match the employee phone number.");
            }

            destination = employeePhone;
        }

        return new SalaryEntryAllocation
        {
            PaymentChannel = channel,
            Amount = amount,
            Destination = destination,
            RecipientType = recipientType!,
            RecipientNameSnapshot = employee.Name,
            Status = "pending"
        };
    }

    private static NormalizedWalletDestination NormalizeWalletDestination(
        string? destination,
        string? type,
        decimal allocationAmount,
        string? employeePhone,
        string label)
    {
        destination = NormalizeOptional(destination);
        var recipientType = WalletSalaryRecipientTypes.NormalizeWalletType(type);

        if (destination is null && recipientType is null)
        {
            if (allocationAmount > 0m)
                throw new PayrollException($"{label} allocation requires a wallet destination and recipient type.");

            return new NormalizedWalletDestination(null, null, null);
        }

        if (recipientType is null)
            throw new PayrollException($"{label} wallet recipient type must be evoId or phoneNumber.");

        if (recipientType == WalletSalaryRecipientTypes.PhoneNumber)
        {
            if (!LibyanPhoneNumber.TryNormalize(employeePhone, out var canonicalEmployeePhone))
            {
                if (allocationAmount > 0m)
                {
                    throw new PayrollException(
                        $"{label} phone allocation requires a valid Libyan employee phone number.");
                }

                // An unused phone rail must not retain a separately entered or stale
                // wallet phone when there is no valid employee phone to derive from.
                return new NormalizedWalletDestination(null, null, null);
            }

            if (destination is not null &&
                (!LibyanPhoneNumber.TryNormalize(destination, out var canonicalDestination) ||
                 !string.Equals(canonicalDestination, canonicalEmployeePhone, StringComparison.Ordinal)))
            {
                throw new PayrollException(
                    $"{label} wallet phone number must match the employee phone number.");
            }

            return new NormalizedWalletDestination(
                canonicalEmployeePhone,
                WalletSalaryRecipientTypes.PhoneNumber,
                canonicalEmployeePhone);
        }

        if (destination is null)
        {
            if (allocationAmount > 0m)
                throw new PayrollException($"{label} allocation requires an EVO ID destination.");

            return new NormalizedWalletDestination(null, null, null);
        }

        if (destination.Length > 34)
            throw new PayrollException($"{label} wallet destination cannot exceed 34 characters.");

        return new NormalizedWalletDestination(
            destination,
            WalletSalaryRecipientTypes.EvoId,
            null);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsAccountNumber(string value) =>
        value.Length == 13 && value.All(char.IsDigit);

    private sealed record NormalizedWalletDestination(
        string? Destination,
        string? RecipientType,
        string? CanonicalEmployeePhone);
}
