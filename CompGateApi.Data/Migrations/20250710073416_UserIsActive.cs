using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RepresentativeId",
                table: "CheckRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RepresentativeId",
                table: "CheckBookRequests",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_CheckRequests_RepresentativeId",
                table: "CheckRequests",
                column: "RepresentativeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckRequests_Representatives_RepresentativeId",
                table: "CheckRequests",
                column: "RepresentativeId",
                principalTable: "Representatives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckRequests_Representatives_RepresentativeId",
                table: "CheckRequests");

            migrationBuilder.DropIndex(
                name: "IX_CheckRequests_RepresentativeId",
                table: "CheckRequests");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RepresentativeId",
                table: "CheckRequests");

            migrationBuilder.AlterColumn<int>(
                name: "RepresentativeId",
                table: "CheckBookRequests",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
