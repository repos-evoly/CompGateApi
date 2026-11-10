using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations;

public partial class AddEurCommissionAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "CommissionAccountEUR",
            table: "Settings",
            type: "nvarchar(13)",
            maxLength: 13,
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "CommissionAccountEUR", table: "Settings");
}
