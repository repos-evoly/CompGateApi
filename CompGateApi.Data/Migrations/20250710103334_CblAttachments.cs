using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class CblAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId",
                table: "CblRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CblRequests_AttachmentId",
                table: "CblRequests",
                column: "AttachmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_CblRequests_Attachments_AttachmentId",
                table: "CblRequests",
                column: "AttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CblRequests_Attachments_AttachmentId",
                table: "CblRequests");

            migrationBuilder.DropIndex(
                name: "IX_CblRequests_AttachmentId",
                table: "CblRequests");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "CblRequests");
        }
    }
}
