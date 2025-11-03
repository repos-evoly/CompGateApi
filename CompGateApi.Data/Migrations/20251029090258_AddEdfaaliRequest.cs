using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEdfaaliRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EdfaaliRequestId",
                table: "Attachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EdfaaliRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    RepresentativeId = table.Column<int>(type: "int", nullable: true),
                    NationalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IdentificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyEnglishName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    StoreAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Street = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ServicePhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankAnnouncementPhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovalTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdfaaliRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EdfaaliRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EdfaaliRequests_Representatives_RepresentativeId",
                        column: x => x.RepresentativeId,
                        principalTable: "Representatives",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EdfaaliRequests_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EdfaaliRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_EdfaaliRequestId",
                table: "Attachments",
                column: "EdfaaliRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EdfaaliRequests_ApprovedByUserId",
                table: "EdfaaliRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EdfaaliRequests_CompanyId",
                table: "EdfaaliRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EdfaaliRequests_RepresentativeId",
                table: "EdfaaliRequests",
                column: "RepresentativeId");

            migrationBuilder.CreateIndex(
                name: "IX_EdfaaliRequests_UserId",
                table: "EdfaaliRequests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_EdfaaliRequests_EdfaaliRequestId",
                table: "Attachments",
                column: "EdfaaliRequestId",
                principalTable: "EdfaaliRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_EdfaaliRequests_EdfaaliRequestId",
                table: "Attachments");

            migrationBuilder.DropTable(
                name: "EdfaaliRequests");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_EdfaaliRequestId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "EdfaaliRequestId",
                table: "Attachments");
        }
    }
}
