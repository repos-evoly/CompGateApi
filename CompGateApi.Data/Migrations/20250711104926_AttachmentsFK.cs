using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentsFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_VisaRequests_VisaRequestId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_CblRequests_Attachments_AttachmentId",
                table: "CblRequests");

            migrationBuilder.DropIndex(
                name: "IX_CblRequests_AttachmentId",
                table: "CblRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId1",
                table: "CblRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CblRequestId",
                table: "Attachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CblRequests_AttachmentId1",
                table: "CblRequests",
                column: "AttachmentId1");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_CblRequestId",
                table: "Attachments",
                column: "CblRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_CblRequests_CblRequestId",
                table: "Attachments",
                column: "CblRequestId",
                principalTable: "CblRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_VisaRequests_VisaRequestId",
                table: "Attachments",
                column: "VisaRequestId",
                principalTable: "VisaRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CblRequests_Attachments_AttachmentId1",
                table: "CblRequests",
                column: "AttachmentId1",
                principalTable: "Attachments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_CblRequests_CblRequestId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_VisaRequests_VisaRequestId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_CblRequests_Attachments_AttachmentId1",
                table: "CblRequests");

            migrationBuilder.DropIndex(
                name: "IX_CblRequests_AttachmentId1",
                table: "CblRequests");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_CblRequestId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "AttachmentId1",
                table: "CblRequests");

            migrationBuilder.DropColumn(
                name: "CblRequestId",
                table: "Attachments");

            migrationBuilder.CreateIndex(
                name: "IX_CblRequests_AttachmentId",
                table: "CblRequests",
                column: "AttachmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_VisaRequests_VisaRequestId",
                table: "Attachments",
                column: "VisaRequestId",
                principalTable: "VisaRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CblRequests_Attachments_AttachmentId",
                table: "CblRequests",
                column: "AttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
