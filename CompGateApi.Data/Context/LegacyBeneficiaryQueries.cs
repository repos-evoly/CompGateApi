using CompGateApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CompGateApi.Data.Context;

/// <summary>
/// Reads the original beneficiary schema without requiring payment-rail migrations.
/// Where a newer schema exists, its rail-only records remain hidden and untouched.
/// </summary>
public static class LegacyBeneficiaryQueries
{
    public static async Task<IQueryable<Beneficiary>> NormalAsync(
        CompGateApiDbContext db, CancellationToken cancellationToken = default)
    {
        // Check metadata before referencing the optional column. Do not infer schema
        // from environment names or cache across databases/connection strings.
        var hasRail = await db.Database.SqlQueryRaw<int>(
            "SELECT CASE WHEN COL_LENGTH(N'dbo.Beneficiaries', N'PaymentRail') IS NULL THEN 0 ELSE 1 END AS [Value]")
            .SingleAsync(cancellationToken);
        return ForSchema(db, hasRail == 1);
    }

    public static IQueryable<Beneficiary> ForSchema(CompGateApiDbContext db, bool hasPaymentRail) =>
        hasPaymentRail
            ? db.Beneficiaries.FromSqlRaw(
                "SELECT * FROM [dbo].[Beneficiaries] WHERE [PaymentRail] = N'Normal'")
            : db.Beneficiaries;
}
