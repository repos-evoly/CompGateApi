using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class TransferAndForeignCurr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                table: "TransferRequests",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecutedAt",
                table: "TransferRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutedByName",
                table: "TransferRequests",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "B2BFixedFeeForeign",
                table: "ServicePackageDetails",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "B2CFixedFeeForeign",
                table: "ServicePackageDetails",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByName",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ExecutedAt",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ExecutedByName",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "B2BFixedFeeForeign",
                table: "ServicePackageDetails");

            migrationBuilder.DropColumn(
                name: "B2CFixedFeeForeign",
                table: "ServicePackageDetails");
        }
    }
}
