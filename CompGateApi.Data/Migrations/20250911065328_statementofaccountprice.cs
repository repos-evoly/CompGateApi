using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class statementofaccountprice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "RtgsRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferRequestId",
                table: "RtgsRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "CreditFacilities",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferRequestId",
                table: "CreditFacilities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "CertifiedBankStatementRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferRequestId",
                table: "CertifiedBankStatementRequests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "RtgsRequests");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "RtgsRequests");

            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "CreditFacilities");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "CreditFacilities");

            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "CertifiedBankStatementRequests");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "CertifiedBankStatementRequests");
        }
    }
}
