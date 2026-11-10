using CompGateApi.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations;

[DbContext(typeof(CompGateApiDbContext))]
[Migration("20261104120000_AddUniqueActiveEmployeeAccount")]
public partial class AddUniqueActiveEmployeeAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Never choose or archive a payroll identity automatically. Resolve any
        // active duplicates explicitly before this schema change is applied.
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM dbo.Employees
                WHERE IsDeleted = 0 AND AccountNumber IS NOT NULL AND AccountNumber <> N''
                GROUP BY CompanyId, AccountNumber HAVING COUNT(*) > 1
            )
                THROW 51000, 'Active employees share an account number within a company. Review and resolve these records before applying AddUniqueActiveEmployeeAccount. No employee records were changed.', 1;
            """);

        migrationBuilder.CreateIndex(
            name: "UX_Employees_CompanyId_AccountNumber_Active",
            table: "Employees",
            columns: new[] { "CompanyId", "AccountNumber" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [AccountNumber] IS NOT NULL AND [AccountNumber] <> N''");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex(
            name: "UX_Employees_CompanyId_AccountNumber_Active",
            table: "Employees");
}
