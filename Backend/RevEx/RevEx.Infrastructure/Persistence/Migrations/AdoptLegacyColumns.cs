using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace RevEx.Infrastructure.Persistence.Migrations;
[DbContext(typeof(PostgresDbContext))]
[Migration("20261030140000_AdoptLegacyColumns")]
public sealed class AdoptLegacyColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Earlier releases may have a length-limited description or no subscription timestamp.
        migrationBuilder.AlterColumn<string>(name: "description", table: "content_history_events", type: "text", nullable: false, oldClrType: typeof(string), oldType: "text");
        migrationBuilder.Sql("ALTER TABLE content_subscriptions ADD COLUMN IF NOT EXISTS subscribed_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP");
    }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
