using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class WalletSalaryAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalaryEntryAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryEntryId = table.Column<int>(type: "int", nullable: false),
                    PaymentChannel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    ClientReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransferResultCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TransferResultReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RawResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsTransferred = table.Column<bool>(type: "bit", nullable: false),
                    TransferredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryEntryAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryEntryAllocations_SalaryEntries_SalaryEntryId",
                        column: x => x.SalaryEntryId,
                        principalTable: "SalaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaryWalletBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryCycleId = table.Column<int>(type: "int", nullable: false),
                    WalletChannel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ShadowAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    BatchReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CoreReferenceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestedTotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SuccessfulTotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FailedTotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalCommission = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OverallStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProviderRequestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversalRequired = table.Column<bool>(type: "bit", nullable: false),
                    ReversalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReversalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReversalBankReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReversalRequestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReversalResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReversalErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryWalletBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryWalletBatches_SalaryCycles_SalaryCycleId",
                        column: x => x.SalaryCycleId,
                        principalTable: "SalaryCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntryAllocations_ClientReference",
                table: "SalaryEntryAllocations",
                column: "ClientReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntryAllocations_SalaryEntryId",
                table: "SalaryEntryAllocations",
                column: "SalaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatches_BatchReference",
                table: "SalaryWalletBatches",
                column: "BatchReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatches_SalaryCycleId",
                table: "SalaryWalletBatches",
                column: "SalaryCycleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalaryEntryAllocations");

            migrationBuilder.DropTable(
                name: "SalaryWalletBatches");
        }
    }
}
