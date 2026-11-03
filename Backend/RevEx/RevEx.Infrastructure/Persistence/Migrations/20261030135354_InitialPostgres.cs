using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RevEx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_history_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    content_id = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    description = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_history_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_notification_deliveries",
                columns: table => new
                {
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_notification_deliveries", x => new { x.event_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "content_subscriptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    content_id = table.Column<int>(type: "integer", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    subscribed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "desktop_installations",
                columns: table => new
                {
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    user_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    user_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_desktop_installations", x => x.installation_id);
                });

            migrationBuilder.CreateTable(
                name: "kafka_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    message_key = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kafka_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "desktop_update_offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    policy = table.Column<string>(type: "jsonb", nullable: false),
                    offered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    user_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    user_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    failure = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_desktop_update_offers", x => x.id);
                    table.ForeignKey(
                        name: "FK_desktop_update_offers_desktop_installations_installation_id",
                        column: x => x.installation_id,
                        principalTable: "desktop_installations",
                        principalColumn: "installation_id");
                });

            migrationBuilder.CreateTable(
                name: "desktop_downloads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    file_name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "started"),
                    user_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    user_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_desktop_downloads", x => x.id);
                    table.ForeignKey(
                        name: "FK_desktop_downloads_desktop_installations_installation_id",
                        column: x => x.installation_id,
                        principalTable: "desktop_installations",
                        principalColumn: "installation_id");
                    table.ForeignKey(
                        name: "FK_desktop_downloads_desktop_update_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "desktop_update_offers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_history_events_content_time",
                table: "content_history_events",
                columns: new[] { "content_id", "occurred_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_content_subscriptions_content_id_user_id",
                table: "content_subscriptions",
                columns: new[] { "content_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_desktop_downloads_installation_id",
                table: "desktop_downloads",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "IX_desktop_downloads_offer_id",
                table: "desktop_downloads",
                column: "offer_id");

            migrationBuilder.CreateIndex(
                name: "ix_desktop_downloads_time",
                table: "desktop_downloads",
                column: "started_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_desktop_installations_user",
                table: "desktop_installations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_desktop_update_offers_installation",
                table: "desktop_update_offers",
                columns: new[] { "installation_id", "offered_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_kafka_outbox_pending",
                table: "kafka_outbox",
                column: "next_attempt_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_history_events");

            migrationBuilder.DropTable(
                name: "content_notification_deliveries");

            migrationBuilder.DropTable(
                name: "content_subscriptions");

            migrationBuilder.DropTable(
                name: "desktop_downloads");

            migrationBuilder.DropTable(
                name: "kafka_outbox");

            migrationBuilder.DropTable(
                name: "desktop_update_offers");

            migrationBuilder.DropTable(
                name: "desktop_installations");
        }
    }
}
