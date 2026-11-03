using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevEx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_comment_sets_created_at_id",
                table: "comment_sets",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_comment_set_id_created_at_id",
                table: "comments",
                columns: new[] { "comment_set_id", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "comment_sets");
        }
    }
}
