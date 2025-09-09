using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefernceToTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SGL2",
                table: "Pricing",
                newName: "GL4");

            migrationBuilder.RenameColumn(
                name: "SGL1",
                table: "Pricing",
                newName: "GL3");

            migrationBuilder.RenameColumn(
                name: "DGL2",
                table: "Pricing",
                newName: "GL2");

            migrationBuilder.RenameColumn(
                name: "DGL1",
                table: "Pricing",
                newName: "GL1");

            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "TransferRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Unit",
                table: "Pricing",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "CheckBookRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferRequestId",
                table: "CheckBookRequests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Pricing");

            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "CheckBookRequests");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "CheckBookRequests");

            migrationBuilder.RenameColumn(
                name: "GL4",
                table: "Pricing",
                newName: "SGL2");

            migrationBuilder.RenameColumn(
                name: "GL3",
                table: "Pricing",
                newName: "SGL1");

            migrationBuilder.RenameColumn(
                name: "GL2",
                table: "Pricing",
                newName: "DGL2");

            migrationBuilder.RenameColumn(
                name: "GL1",
                table: "Pricing",
                newName: "DGL1");
        }
    }
}
