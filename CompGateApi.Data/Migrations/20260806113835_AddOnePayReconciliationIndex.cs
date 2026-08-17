using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnePayReconciliationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OnePayTransfers_Status_NextStatusCheckAt",
                table: "OnePayTransfers",
                columns: new[] { "Status", "NextStatusCheckAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OnePayTransfers_Status_NextStatusCheckAt",
                table: "OnePayTransfers");
        }
    }
}
