using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class GroupIdtotransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupId",
                table: "TransferRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Permissions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Permissions");
        }
    }
}
