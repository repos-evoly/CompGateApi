using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefIdSalaryCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "TransactionCategories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "SalaryCycles",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankResponseRaw",
                table: "SalaryCycles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LetterOfGuarenteePct",
                table: "CreditFacilities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "CreditFacilities",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "TransactionCategories");

            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "BankResponseRaw",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "LetterOfGuarenteePct",
                table: "CreditFacilities");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "CreditFacilities");
        }
    }
}
