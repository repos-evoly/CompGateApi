using CompGateApi.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    [DbContext(typeof(CompGateApiDbContext))]
    [Migration("20260810190000_AddOnePayProviderDiagnostics")]
    public partial class AddOnePayProviderDiagnostics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderGeneralError",
                table: "OnePayTransfers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProviderIsFinished",
                table: "OnePayTransfers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProviderIsTransactionSuccess",
                table: "OnePayTransfers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProviderMainSuccess",
                table: "OnePayTransfers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProviderStillUnderProcessing",
                table: "OnePayTransfers",
                type: "bit",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderGeneralError",
                table: "OnePayTransfers");

            migrationBuilder.DropColumn(
                name: "ProviderIsFinished",
                table: "OnePayTransfers");

            migrationBuilder.DropColumn(
                name: "ProviderIsTransactionSuccess",
                table: "OnePayTransfers");

            migrationBuilder.DropColumn(
                name: "ProviderMainSuccess",
                table: "OnePayTransfers");

            migrationBuilder.DropColumn(
                name: "ProviderStillUnderProcessing",
                table: "OnePayTransfers");
        }
    }
}
