using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepresentativeAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Representatives",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PhotoFileName",
                table: "Representatives",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Representatives",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Representatives");

            migrationBuilder.DropColumn(
                name: "PhotoFileName",
                table: "Representatives");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Representatives");
        }
    }
}
