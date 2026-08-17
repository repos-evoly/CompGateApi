using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    // Ordered after AddRailAwareBeneficiaries, which creates the indexed columns.
    /// <inheritdoc />
    public partial class EnforceUniqueRailBeneficiaryDestinations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_Beneficiaries_ActiveRailDestination",
                table: "Beneficiaries",
                columns: new[] { "CompanyId", "PaymentRail", "InstitutionId", "AccountNumber" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [InstitutionId] IS NOT NULL AND [PaymentRail] IN (N'OnePay', N'LyPay')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Beneficiaries_ActiveRailDestination",
                table: "Beneficiaries");
        }
    }
}
