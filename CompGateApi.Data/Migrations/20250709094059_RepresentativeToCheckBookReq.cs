using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepresentativeToCheckBookReq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RepresentativeId",
                table: "CheckBookRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CheckBookRequests_RepresentativeId",
                table: "CheckBookRequests",
                column: "RepresentativeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckBookRequests_Representatives_RepresentativeId",
                table: "CheckBookRequests",
                column: "RepresentativeId",
                principalTable: "Representatives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckBookRequests_Representatives_RepresentativeId",
                table: "CheckBookRequests");

            migrationBuilder.DropIndex(
                name: "IX_CheckBookRequests_RepresentativeId",
                table: "CheckBookRequests");

            migrationBuilder.DropColumn(
                name: "RepresentativeId",
                table: "CheckBookRequests");
        }
    }
}
