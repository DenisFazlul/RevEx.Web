using Microsoft.EntityFrameworkCore.Migrations;

namespace RevEx.Infrastructure.Persistence.Migrations;

public partial class AddContentFreeze : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_frozen",
            table: "contents",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "is_frozen", table: "contents");
    }
}
