using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnePayTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnePayTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ApprovedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FromAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    FromAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FromAccountPhoneNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToInstitutionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToInstitutionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    ToAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HostReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DhbReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AgreementReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BankReferenceNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderReturnMessageCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderReturnMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProviderTransactionStatus = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StatusCheckAttempts = table.Column<int>(type: "int", nullable: false),
                    LastStatusCheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextStatusCheckAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StatusCheckClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExecutionClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnePayTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnePayTransfers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnePayTransfers_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnePayTransfers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnePayValidationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FromAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    FromAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FromAccountPhoneNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToInstitutionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToInstitutionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    ToAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    HostReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DhbReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AgreementReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BankReferenceNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnePayValidationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnePayValidationSessions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnePayValidationSessions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnePayTransfers_ApprovedByUserId",
                table: "OnePayTransfers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnePayTransfers_CompanyId_Status_NextStatusCheckAt",
                table: "OnePayTransfers",
                columns: new[] { "CompanyId", "Status", "NextStatusCheckAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OnePayTransfers_CreatedByUserId",
                table: "OnePayTransfers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnePayTransfers_ReferenceNo",
                table: "OnePayTransfers",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnePayValidationSessions_CompanyId_CreatedByUserId_ExpiresAt",
                table: "OnePayValidationSessions",
                columns: new[] { "CompanyId", "CreatedByUserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OnePayValidationSessions_CreatedByUserId",
                table: "OnePayValidationSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnePayValidationSessions_ReferenceNo",
                table: "OnePayValidationSessions",
                column: "ReferenceNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnePayTransfers");

            migrationBuilder.DropTable(
                name: "OnePayValidationSessions");
        }
    }
}
