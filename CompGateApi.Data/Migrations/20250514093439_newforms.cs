using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class newforms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForeignTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ToBank = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Branch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResidentSupplierName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ResidentSupplierNationality = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NonResidentPassportNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlaceOfIssue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateOfIssue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NonResidentNationality = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NonResidentAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TransferAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ToCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BeneficiaryName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BeneficiaryAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ExternalBankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ExternalBankAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TransferToAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TransferToAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AccountHolderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PermanentAddress = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PurposeOfTransfer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForeignTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForeignTransfers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisaRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccountHolderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NationalId = table.Column<long>(type: "bigint", nullable: true),
                    PhoneNumberLinkedToNationalId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Cbl = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CardMovementApproval = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CardUsingAcknowledgment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ForeignAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LocalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Pldedge = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisaRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisaRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForeignTransfers_UserId",
                table: "ForeignTransfers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisaRequests_UserId",
                table: "VisaRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForeignTransfers");

            migrationBuilder.DropTable(
                name: "VisaRequests");
        }
    }
}
