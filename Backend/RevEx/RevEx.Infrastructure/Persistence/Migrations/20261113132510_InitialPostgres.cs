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
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comment_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Название набора комментариев."),
                    author_id = table.Column<string>(type: "text", nullable: false, comment: "Неизменяемый идентификатор создателя из токена."),
                    author = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comment_sets", x => x.id);
                },
                comment: "Наборы комментариев: отдельные обсуждения.");

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
                name: "content_statuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_statuses", x => x.id);
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
                name: "tag_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false, comment: "Текст комментария."),
                    author_id = table.Column<string>(type: "text", nullable: false, comment: "Неизменяемый идентификатор автора из токена."),
                    author = table.Column<string>(type: "text", nullable: false, comment: "Отображаемое имя автора на момент создания."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Время создания в UTC."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Время последнего изменения в UTC; null до первого редактирования.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_comments_comment_sets_comment_set_id",
                        column: x => x.comment_set_id,
                        principalTable: "comment_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Комментарии, принадлежащие одному набору.");

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
                name: "tags",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tag_group_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                    table.ForeignKey(
                        name: "FK_tags_tag_groups_tag_group_id",
                        column: x => x.tag_group_id,
                        principalTable: "tag_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "content_file_metadata",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_id = table.Column<int>(type: "integer", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_file_metadata", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_files",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    url = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    extension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    content_version_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    content_status_id = table.Column<int>(type: "integer", nullable: false),
                    preview_file_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contents", x => x.id);
                    table.ForeignKey(
                        name: "FK_contents_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contents_content_files_preview_file_id",
                        column: x => x.preview_file_id,
                        principalTable: "content_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contents_content_statuses_content_status_id",
                        column: x => x.content_status_id,
                        principalTable: "content_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "content_tags",
                columns: table => new
                {
                    contents_id = table.Column<int>(type: "integer", nullable: false),
                    tags_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_tags", x => new { x.contents_id, x.tags_id });
                    table.ForeignKey(
                        name: "FK_content_tags_contents_contents_id",
                        column: x => x.contents_id,
                        principalTable: "contents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_content_tags_tags_tags_id",
                        column: x => x.tags_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_versions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    vid = table.Column<Guid>(type: "uuid", nullable: true),
                    content_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    publish_status = table.Column<int>(type: "integer", nullable: false),
                    platform = table.Column<int>(type: "integer", nullable: false),
                    application = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    application_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_content_versions_contents_content_id",
                        column: x => x.content_id,
                        principalTable: "contents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comment_sets_created_at_id",
                table: "comment_sets",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_comment_set_id_created_at_id",
                table: "comments",
                columns: new[] { "comment_set_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_content_file_metadata_file_id",
                table: "content_file_metadata",
                column: "file_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_files_content_version_id",
                table: "content_files",
                column: "content_version_id");

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
                name: "IX_content_tags_tags_id",
                table: "content_tags",
                column: "tags_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_versions_content_id",
                table: "content_versions",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_versions_vid",
                table: "content_versions",
                column: "vid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contents_category_id",
                table: "contents",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_contents_cid",
                table: "contents",
                column: "cid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contents_content_status_id",
                table: "contents",
                column: "content_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_contents_preview_file_id",
                table: "contents",
                column: "preview_file_id");

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

            migrationBuilder.CreateIndex(
                name: "IX_tags_tag_group_id",
                table: "tags",
                column: "tag_group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_content_file_metadata_content_files_file_id",
                table: "content_file_metadata",
                column: "file_id",
                principalTable: "content_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_content_files_content_versions_content_version_id",
                table: "content_files",
                column: "content_version_id",
                principalTable: "content_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contents_content_files_preview_file_id",
                table: "contents");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "content_file_metadata");

            migrationBuilder.DropTable(
                name: "content_history_events");

            migrationBuilder.DropTable(
                name: "content_notification_deliveries");

            migrationBuilder.DropTable(
                name: "content_subscriptions");

            migrationBuilder.DropTable(
                name: "content_tags");

            migrationBuilder.DropTable(
                name: "desktop_downloads");

            migrationBuilder.DropTable(
                name: "kafka_outbox");

            migrationBuilder.DropTable(
                name: "comment_sets");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "desktop_update_offers");

            migrationBuilder.DropTable(
                name: "tag_groups");

            migrationBuilder.DropTable(
                name: "desktop_installations");

            migrationBuilder.DropTable(
                name: "content_files");

            migrationBuilder.DropTable(
                name: "content_versions");

            migrationBuilder.DropTable(
                name: "contents");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "content_statuses");
        }
    }
}
