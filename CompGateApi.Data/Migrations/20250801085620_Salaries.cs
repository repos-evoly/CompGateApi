using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class Salaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CblRequests_Attachments_AttachmentId1",
                table: "CblRequests");

            migrationBuilder.DropIndex(
                name: "IX_CblRequests_AttachmentId1",
                table: "CblRequests");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "CblRequests");

            migrationBuilder.DropColumn(
                name: "AttachmentId1",
                table: "CblRequests");

            migrationBuilder.AlterColumn<decimal>(
                name: "CommissionAmount",
                table: "TransferRequests",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "B2CCommissionPct",
                table: "ServicePackageDetails",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "B2BCommissionPct",
                table: "ServicePackageDetails",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SendSalary = table.Column<bool>(type: "bit", nullable: false),
                    CanPost = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaryCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    SalaryMonth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    PostedByUserId = table.Column<int>(type: "int", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryCycles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryCycleId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsTransferred = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalaryEntries_SalaryCycles_SalaryCycleId",
                        column: x => x.SalaryCycleId,
                        principalTable: "SalaryCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId",
                table: "Employees",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryCycles_CompanyId",
                table: "SalaryCycles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntries_EmployeeId",
                table: "SalaryEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntries_SalaryCycleId",
                table: "SalaryEntries",
                column: "SalaryCycleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalaryEntries");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "SalaryCycles");

            migrationBuilder.AlterColumn<decimal>(
                name: "CommissionAmount",
                table: "TransferRequests",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "B2CCommissionPct",
                table: "ServicePackageDetails",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "B2BCommissionPct",
                table: "ServicePackageDetails",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId",
                table: "CblRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId1",
                table: "CblRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CblRequests_AttachmentId1",
                table: "CblRequests",
                column: "AttachmentId1");

            migrationBuilder.AddForeignKey(
                name: "FK_CblRequests_Attachments_AttachmentId1",
                table: "CblRequests",
                column: "AttachmentId1",
                principalTable: "Attachments",
                principalColumn: "Id");
        }
    }
}
