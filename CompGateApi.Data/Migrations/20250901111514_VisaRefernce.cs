using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class VisaRefernce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovalTimestamp",
                table: "VisaRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "VisaRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "VisaRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "VisaRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TransferRequestId",
                table: "VisaRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisaId",
                table: "VisaRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "CheckRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferRequestId",
                table: "CheckRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisaRequests_ApprovedByUserId",
                table: "VisaRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisaRequests_TransferRequestId",
                table: "VisaRequests",
                column: "TransferRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_VisaRequests_VisaId",
                table: "VisaRequests",
                column: "VisaId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckRequests_TransferRequestId",
                table: "CheckRequests",
                column: "TransferRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckRequests_TransferRequests_TransferRequestId",
                table: "CheckRequests",
                column: "TransferRequestId",
                principalTable: "TransferRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VisaRequests_TransferRequests_TransferRequestId",
                table: "VisaRequests",
                column: "TransferRequestId",
                principalTable: "TransferRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VisaRequests_Users_ApprovedByUserId",
                table: "VisaRequests",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VisaRequests_Visas_VisaId",
                table: "VisaRequests",
                column: "VisaId",
                principalTable: "Visas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CheckRequests_TransferRequests_TransferRequestId",
                table: "CheckRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VisaRequests_TransferRequests_TransferRequestId",
                table: "VisaRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VisaRequests_Users_ApprovedByUserId",
                table: "VisaRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VisaRequests_Visas_VisaId",
                table: "VisaRequests");

            migrationBuilder.DropIndex(
                name: "IX_VisaRequests_ApprovedByUserId",
                table: "VisaRequests");

            migrationBuilder.DropIndex(
                name: "IX_VisaRequests_TransferRequestId",
                table: "VisaRequests");

            migrationBuilder.DropIndex(
                name: "IX_VisaRequests_VisaId",
                table: "VisaRequests");

            migrationBuilder.DropIndex(
                name: "IX_CheckRequests_TransferRequestId",
                table: "CheckRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalTimestamp",
                table: "VisaRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "VisaRequests");

            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "VisaRequests");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "VisaRequests");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "VisaRequests");

            migrationBuilder.DropColumn(
                name: "VisaId",
                table: "VisaRequests");

            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "CheckRequests");

            migrationBuilder.DropColumn(
                name: "TransferRequestId",
                table: "CheckRequests");
        }
    }
}
