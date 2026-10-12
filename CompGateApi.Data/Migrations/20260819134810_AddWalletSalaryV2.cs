using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletSalaryV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalaryCycles_CompanyId",
                table: "SalaryCycles");

            migrationBuilder.AddColumn<string>(
                name: "BankFeeError",
                table: "SalaryCycles",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankFeeStatus",
                table: "SalaryCycles",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "notRequired");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostingClaimedAt",
                table: "SalaryCycles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostingCompletedAt",
                table: "SalaryCycles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostingStartedAt",
                table: "SalaryCycles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostingStatus",
                table: "SalaryCycles",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "notPosted");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SalaryCycles",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "Employees",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(34)",
                oldMaxLength: 34);

            migrationBuilder.AddColumn<string>(
                name: "BcdWalletRecipientType",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvoWalletRecipientType",
                table: "Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalaryEntryAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryEntryId = table.Column<int>(type: "int", nullable: false),
                    PaymentChannel = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsTransferred = table.Column<bool>(type: "bit", nullable: false),
                    TransferredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostedByUserId = table.Column<int>(type: "int", nullable: true),
                    TransferResultCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TransferResultReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderTxCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProviderResponseRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReversalStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReversalReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReversalError = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                    Rail = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ShadowAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CoreFundingStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CoreFundingReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CoreFundingResponseRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedTotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SuccessfulTotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FailedTotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalCommission = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SenderCommission = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceiverCommission = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProviderBatchId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderRequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderRequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispatchClassification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RetryAllowed = table.Column<bool>(type: "bit", nullable: false),
                    LastErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DispatchStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DispatchCompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextCheckAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CheckClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReversalStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReversalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReversalReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReversalResponseRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReversalError = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryWalletBatchAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryWalletBatchId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    AttemptType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DispatchClassification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResultStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryWalletBatchAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryWalletBatchAttempts_SalaryWalletBatches_SalaryWalletBatchId",
                        column: x => x.SalaryWalletBatchId,
                        principalTable: "SalaryWalletBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaryWalletBatchItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryWalletBatchId = table.Column<int>(type: "int", nullable: false),
                    SalaryEntryAllocationId = table.Column<int>(type: "int", nullable: false),
                    RequestIndex = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderTxCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProviderResponseRaw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryWalletBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryWalletBatchItems_SalaryEntryAllocations_SalaryEntryAllocationId",
                        column: x => x.SalaryEntryAllocationId,
                        principalTable: "SalaryEntryAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryWalletBatchItems_SalaryWalletBatches_SalaryWalletBatchId",
                        column: x => x.SalaryWalletBatchId,
                        principalTable: "SalaryWalletBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalaryWalletManualReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryWalletBatchId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryWalletManualReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryWalletManualReviews_SalaryWalletBatches_SalaryWalletBatchId",
                        column: x => x.SalaryWalletBatchId,
                        principalTable: "SalaryWalletBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Preserve legacy salary cycles on the existing bank-only path.
            // New or explicitly edited cycles create allocation snapshots;
            // historical cycles are intentionally not converted in-place.
            migrationBuilder.Sql(
                """
                UPDATE Employees
                SET EvoWalletRecipientType = CASE
                        WHEN LTRIM(RTRIM(EvoWallet)) LIKE '+%' THEN 'phoneNumber'
                        ELSE 'evoId'
                    END
                WHERE NULLIF(LTRIM(RTRIM(EvoWallet)), '') IS NOT NULL
                  AND EvoWalletRecipientType IS NULL;

                UPDATE Employees
                SET BcdWalletRecipientType = CASE
                        WHEN LTRIM(RTRIM(BcdWallet)) LIKE '+%' THEN 'phoneNumber'
                        ELSE 'evoId'
                    END
                WHERE NULLIF(LTRIM(RTRIM(BcdWallet)), '') IS NOT NULL
                  AND BcdWalletRecipientType IS NULL;

                UPDATE sc
                SET PostingStatus = CASE
                        WHEN sc.PostedAt IS NULL THEN 'notPosted'
                        WHEN EXISTS (
                            SELECT 1 FROM SalaryEntries se
                            WHERE se.SalaryCycleId = sc.Id AND se.IsTransferred = 1)
                         AND EXISTS (
                            SELECT 1 FROM SalaryEntries se
                            WHERE se.SalaryCycleId = sc.Id AND se.IsTransferred = 0)
                            THEN 'partiallyCompleted'
                        WHEN EXISTS (
                            SELECT 1 FROM SalaryEntries se
                            WHERE se.SalaryCycleId = sc.Id AND se.IsTransferred = 1)
                         AND NOT EXISTS (
                            SELECT 1 FROM SalaryEntries se
                            WHERE se.SalaryCycleId = sc.Id AND se.IsTransferred = 0)
                            THEN 'completed'
                        ELSE 'failed'
                    END,
                    PostingStartedAt = CASE
                        WHEN sc.PostedAt IS NULL THEN NULL
                        ELSE TODATETIMEOFFSET(sc.PostedAt, '+00:00')
                    END,
                    PostingCompletedAt = CASE
                        WHEN sc.PostedAt IS NULL THEN NULL
                        ELSE TODATETIMEOFFSET(sc.PostedAt, '+00:00')
                    END,
                    BankFeeStatus = CASE
                        WHEN NULLIF(LTRIM(RTRIM(sc.BankFeeReference)), '') IS NULL THEN 'notRequired'
                        ELSE 'legacyRecorded'
                    END
                FROM SalaryCycles sc;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryCycles_CompanyId_PostingStatus",
                table: "SalaryCycles",
                columns: new[] { "CompanyId", "PostingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntryAllocations_SalaryEntryId_PaymentChannel",
                table: "SalaryEntryAllocations",
                columns: new[] { "SalaryEntryId", "PaymentChannel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryEntryAllocations_Status_PaymentChannel",
                table: "SalaryEntryAllocations",
                columns: new[] { "Status", "PaymentChannel" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatchAttempts_SalaryWalletBatchId_AttemptNumber",
                table: "SalaryWalletBatchAttempts",
                columns: new[] { "SalaryWalletBatchId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatches_SalaryCycleId_Rail_Purpose_SequenceNumber",
                table: "SalaryWalletBatches",
                columns: new[] { "SalaryCycleId", "Rail", "Purpose", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatches_Status_NextCheckAt",
                table: "SalaryWalletBatches",
                columns: new[] { "Status", "NextCheckAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatchItems_SalaryEntryAllocationId",
                table: "SalaryWalletBatchItems",
                column: "SalaryEntryAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatchItems_SalaryWalletBatchId_RequestIndex",
                table: "SalaryWalletBatchItems",
                columns: new[] { "SalaryWalletBatchId", "RequestIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatchItems_SalaryWalletBatchId_SalaryEntryAllocationId",
                table: "SalaryWalletBatchItems",
                columns: new[] { "SalaryWalletBatchId", "SalaryEntryAllocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletManualReviews_SalaryWalletBatchId",
                table: "SalaryWalletManualReviews",
                column: "SalaryWalletBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletManualReviews_Status_CreatedAt",
                table: "SalaryWalletManualReviews",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalaryWalletBatchAttempts");

            migrationBuilder.DropTable(
                name: "SalaryWalletBatchItems");

            migrationBuilder.DropTable(
                name: "SalaryWalletManualReviews");

            migrationBuilder.DropTable(
                name: "SalaryEntryAllocations");

            migrationBuilder.DropTable(
                name: "SalaryWalletBatches");

            migrationBuilder.DropIndex(
                name: "IX_SalaryCycles_CompanyId_PostingStatus",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "BankFeeError",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "BankFeeStatus",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "PostingClaimedAt",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "PostingCompletedAt",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "PostingStartedAt",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "PostingStatus",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalaryCycles");

            migrationBuilder.DropColumn(
                name: "BcdWalletRecipientType",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "EvoWalletRecipientType",
                table: "Employees");

            migrationBuilder.Sql(
                "UPDATE Employees SET AccountNumber = '' WHERE AccountNumber IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "Employees",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(34)",
                oldMaxLength: 34,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryCycles_CompanyId",
                table: "SalaryCycles",
                column: "CompanyId");
        }
    }
}
