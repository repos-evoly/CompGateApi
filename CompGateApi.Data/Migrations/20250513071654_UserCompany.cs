using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCompanyAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KycBranchId",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycCity",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycLegalCompanyName",
                table: "Users",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycLegalCompanyNameLt",
                table: "Users",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycMobile",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycNationality",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KycRequestedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KycReviewedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KycStatus",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "KycStatusMessage",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCompanyAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycBranchId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycCity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycLegalCompanyName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycLegalCompanyNameLt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycMobile",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycNationality",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycRequestedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycReviewedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KycStatusMessage",
                table: "Users");
        }
    }
}
