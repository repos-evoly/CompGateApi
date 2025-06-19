using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class EconomicSect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EconomicSectorId",
                table: "TransferRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Rate",
                table: "TransferRequests",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TransferMode",
                table: "TransferRequests",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EconomicSectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicSectors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_EconomicSectorId",
                table: "TransferRequests",
                column: "EconomicSectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_EconomicSectors_EconomicSectorId",
                table: "TransferRequests",
                column: "EconomicSectorId",
                principalTable: "EconomicSectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_EconomicSectors_EconomicSectorId",
                table: "TransferRequests");

            migrationBuilder.DropTable(
                name: "EconomicSectors");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_EconomicSectorId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "EconomicSectorId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "Rate",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "TransferMode",
                table: "TransferRequests");
        }
    }
}
