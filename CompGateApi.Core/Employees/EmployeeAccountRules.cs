using CompGateApi.Core.Errors;

namespace CompGateApi.Core.Employees;

public static class EmployeeAccountRules
{
    public const string ActiveAccountIndex = "UX_Employees_CompanyId_AccountNumber_Active";

    public static string Normalize(string? account)
    {
        var normalized = (account ?? string.Empty).Trim().Replace(" ", string.Empty);
        return normalized.EndsWith(".0", StringComparison.Ordinal) ? normalized[..^2] : normalized;
    }

    public static Dictionary<string, Employee> SelectImportTargets(IEnumerable<Employee> employees)
    {
        var targets = new Dictionary<string, Employee>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in employees.Where(e => !string.IsNullOrWhiteSpace(e.AccountNumber))
                     .GroupBy(e => Normalize(e.AccountNumber), StringComparer.OrdinalIgnoreCase))
        {
            var active = group.Where(e => !e.IsDeleted).OrderBy(e => e.Id).ToList();
            if (active.Count > 1)
                throw new EmployeeAccountConflictException(
                    $"Account {group.Key} belongs to multiple active employees (IDs: {string.Join(", ", active.Select(e => e.Id))}). " +
                    "Resolve these duplicate employee records before uploading. No employees were changed.");

            // Keep the active identity and its salary history. If all are archived,
            // reactivate the most recently created identity without merging records.
            targets[group.Key] = active.SingleOrDefault() ?? group.OrderByDescending(e => e.Id).First();
        }
        return targets;
    }
}
