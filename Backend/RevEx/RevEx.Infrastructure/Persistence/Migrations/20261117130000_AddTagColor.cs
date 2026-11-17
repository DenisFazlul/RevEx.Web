using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RevEx.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261117130000_AddTagColor")]
public sealed class AddTagColor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "color",
            table: "tags",
            type: "character varying(7)",
            maxLength: 7,
            nullable: false,
            defaultValue: "#75451D");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "color", table: "tags");
    }
}
