using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SalaryUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankLineResponseRaw",
                table: "SalaryEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferResultCode",
                table: "SalaryEntries",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferResultReason",
                table: "SalaryEntries",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankBatchHistoryJson",
                table: "SalaryCycles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BcdWallet",
                table: "Employees",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvoWallet",
                table: "Employees",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankLineResponseRaw",
                table: "SalaryEntries");

            migrationBuilder.DropColumn(
                name: "TransferResultCode",
                table: "SalaryEntries");

            migrationBuilder.DropColumn(
                name: "TransferResultReason",
                table: "SalaryEntries");

            migrationBuilder.DropColumn(
                name: "BankBatchHistoryJson",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "BcdWallet",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "EvoWallet",
                table: "Employees");
        }
    }
}
