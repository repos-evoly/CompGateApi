using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class Visa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "Attachments",
                type: "int",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldMaxLength: 8);

            migrationBuilder.AddColumn<int>(
                name: "VisaId",
                table: "Attachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Visas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_VisaId",
                table: "Attachments",
                column: "VisaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Visas_VisaId",
                table: "Attachments",
                column: "VisaId",
                principalTable: "Visas",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Visas_VisaId",
                table: "Attachments");

            migrationBuilder.DropTable(
                name: "Visas");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_VisaId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "VisaId",
                table: "Attachments");

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "Attachments",
                type: "int",
                maxLength: 8,
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldMaxLength: 8,
                oldNullable: true);
        }
    }
}
