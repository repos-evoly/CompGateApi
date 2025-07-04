using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReasonsToForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "VisaRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "RtgsRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "ForeignTransfers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CheckRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CheckBookRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CblRequests",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "VisaRequests");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "RtgsRequests");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "ForeignTransfers");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CheckRequests");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CheckBookRequests");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CblRequests");
        }
    }
}
