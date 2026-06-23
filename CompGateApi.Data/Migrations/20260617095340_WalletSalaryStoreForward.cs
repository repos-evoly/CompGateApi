using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class WalletSalaryStoreForward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "SalaryWalletBatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "SalaryWalletBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorMessage",
                table: "SalaryWalletBatches",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "SalaryWalletBatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                table: "SalaryWalletBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "SalaryWalletBatches",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "SalaryWalletBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PostedByUserId",
                table: "SalaryWalletBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationMode",
                table: "SalaryWalletBatches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "payment_retry");

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationStatus",
                table: "SalaryWalletBatches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "not_required");

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "SalaryWalletBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalaryWalletBatchAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryWalletBatchId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    AttemptType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResultStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                name: "SalaryWalletManualReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalaryWalletBatchId = table.Column<int>(type: "int", nullable: false),
                    SalaryCycleId = table.Column<int>(type: "int", nullable: false),
                    WalletChannel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BatchReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CoreReferenceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ShadowAccount = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnresolvedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReasonMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ProviderRequestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProviderResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryWalletManualReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryWalletManualReviews_SalaryCycles_SalaryCycleId",
                        column: x => x.SalaryCycleId,
                        principalTable: "SalaryCycles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalaryWalletManualReviews_SalaryWalletBatches_SalaryWalletBatchId",
                        column: x => x.SalaryWalletBatchId,
                        principalTable: "SalaryWalletBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatches_ReconciliationStatus_NextAttemptAt",
                table: "SalaryWalletBatches",
                columns: new[] { "ReconciliationStatus", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletBatchAttempts_SalaryWalletBatchId",
                table: "SalaryWalletBatchAttempts",
                column: "SalaryWalletBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryWalletManualReviews_SalaryCycleId",
                table: "SalaryWalletManualReviews",
                column: "SalaryCycleId");

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
                name: "SalaryWalletManualReviews");

            migrationBuilder.DropIndex(
                name: "IX_SalaryWalletBatches_ReconciliationStatus_NextAttemptAt",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "LastErrorMessage",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "PostedByUserId",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "ReconciliationMode",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "ReconciliationStatus",
                table: "SalaryWalletBatches");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "SalaryWalletBatches");
        }
    }
}
