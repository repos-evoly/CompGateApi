using CompGateApi.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    [DbContext(typeof(CompGateApiDbContext))]
    [Migration("20260821143000_AddEmployeeDefaultSalaryAllocations")]
    public partial class AddEmployeeDefaultSalaryAllocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A previous, retired UAT implementation created these physical
            // columns without a migration in the current history. Adopt and
            // normalize them when present; create them on a clean database.
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Employees', N'AccountAllocationAmount') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Employees]
                    ADD [AccountAllocationAmount] decimal(18,3) NOT NULL
                        CONSTRAINT [DF_Employees_AccountAllocationAmount] DEFAULT (0);
                END
                ELSE
                BEGIN
                    UPDATE [dbo].[Employees]
                    SET [AccountAllocationAmount] = 0
                    WHERE [AccountAllocationAmount] IS NULL;

                    ALTER TABLE [dbo].[Employees]
                    ALTER COLUMN [AccountAllocationAmount] decimal(18,3) NOT NULL;
                END;

                IF COL_LENGTH(N'dbo.Employees', N'EvoAllocationAmount') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Employees]
                    ADD [EvoAllocationAmount] decimal(18,3) NOT NULL
                        CONSTRAINT [DF_Employees_EvoAllocationAmount] DEFAULT (0);
                END
                ELSE
                BEGIN
                    UPDATE [dbo].[Employees]
                    SET [EvoAllocationAmount] = 0
                    WHERE [EvoAllocationAmount] IS NULL;

                    ALTER TABLE [dbo].[Employees]
                    ALTER COLUMN [EvoAllocationAmount] decimal(18,3) NOT NULL;
                END;

                IF COL_LENGTH(N'dbo.Employees', N'BcdAllocationAmount') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Employees]
                    ADD [BcdAllocationAmount] decimal(18,3) NOT NULL
                        CONSTRAINT [DF_Employees_BcdAllocationAmount] DEFAULT (0);
                END
                ELSE
                BEGIN
                    UPDATE [dbo].[Employees]
                    SET [BcdAllocationAmount] = 0
                    WHERE [BcdAllocationAmount] IS NULL;

                    ALTER TABLE [dbo].[Employees]
                    ALTER COLUMN [BcdAllocationAmount] decimal(18,3) NOT NULL;
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumnSql("AccountAllocationAmount"));
            migrationBuilder.Sql(DropColumnSql("EvoAllocationAmount"));
            migrationBuilder.Sql(DropColumnSql("BcdAllocationAmount"));
        }

        private static string DropColumnSql(string columnName) =>
            $$"""
              IF COL_LENGTH(N'dbo.Employees', N'{{columnName}}') IS NOT NULL
              BEGIN
                  DECLARE @constraintName sysname;
                  SELECT @constraintName = dc.[name]
                  FROM sys.default_constraints AS dc
                  INNER JOIN sys.columns AS c
                      ON c.[default_object_id] = dc.[object_id]
                  WHERE c.[object_id] = OBJECT_ID(N'dbo.Employees')
                    AND c.[name] = N'{{columnName}}';

                  IF @constraintName IS NOT NULL
                      EXEC(N'ALTER TABLE [dbo].[Employees] DROP CONSTRAINT [' +
                           REPLACE(@constraintName, N']', N']]') + N']');

                  ALTER TABLE [dbo].[Employees] DROP COLUMN [{{columnName}}];
              END;
              """;
    }
}
