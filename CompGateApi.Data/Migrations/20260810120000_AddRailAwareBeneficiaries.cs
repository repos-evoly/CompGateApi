using CompGateApi.Data.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    [DbContext(typeof(CompGateApiDbContext))]
    [Migration("20260810120000_AddRailAwareBeneficiaries")]
    public partial class AddRailAwareBeneficiaries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Beneficiaries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionId",
                table: "Beneficiaries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionName",
                table: "Beneficiaries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentRail",
                table: "Beneficiaries",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "ProviderInstitutionReference",
                table: "Beneficiaries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Beneficiaries",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            // Existing beneficiaries are ordinary internal-transfer beneficiaries.
            migrationBuilder.Sql(
                "UPDATE [Beneficiaries] SET [PaymentRail] = N'Normal' " +
                "WHERE [PaymentRail] IS NULL OR LTRIM(RTRIM([PaymentRail])) = N'';");

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_CompanyId_PaymentRail_IsDeleted",
                table: "Beneficiaries",
                columns: new[] { "CompanyId", "PaymentRail", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiaries_CreatedByUserId",
                table: "Beneficiaries",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Beneficiaries_Users_CreatedByUserId",
                table: "Beneficiaries",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Beneficiaries_Users_CreatedByUserId",
                table: "Beneficiaries");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiaries_CompanyId_PaymentRail_IsDeleted",
                table: "Beneficiaries");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiaries_CreatedByUserId",
                table: "Beneficiaries");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Beneficiaries");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "Beneficiaries");

            migrationBuilder.DropColumn(
                name: "InstitutionName",
                table: "Beneficiaries");

            migrationBuilder.DropColumn(
                name: "PaymentRail",
                table: "Beneficiaries");

            migrationBuilder.DropColumn(
                name: "ProviderInstitutionReference",
                table: "Beneficiaries");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Beneficiaries");
        }
    }
}
