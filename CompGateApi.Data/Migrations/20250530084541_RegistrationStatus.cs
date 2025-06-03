using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KycStatusMessage",
                table: "Companies",
                newName: "RegistrationStatusMessage");

            migrationBuilder.RenameColumn(
                name: "KycStatus",
                table: "Companies",
                newName: "RegistrationStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RegistrationStatusMessage",
                table: "Companies",
                newName: "KycStatusMessage");

            migrationBuilder.RenameColumn(
                name: "RegistrationStatus",
                table: "Companies",
                newName: "KycStatus");
        }
    }
}
