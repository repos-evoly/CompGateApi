using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class VisaRequestAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId",
                table: "VisaRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisaRequestId",
                table: "Attachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_VisaRequestId",
                table: "Attachments",
                column: "VisaRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_VisaRequests_VisaRequestId",
                table: "Attachments",
                column: "VisaRequestId",
                principalTable: "VisaRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_VisaRequests_VisaRequestId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_VisaRequestId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "VisaRequests");

            migrationBuilder.DropColumn(
                name: "VisaRequestId",
                table: "Attachments");
        }
    }
}
