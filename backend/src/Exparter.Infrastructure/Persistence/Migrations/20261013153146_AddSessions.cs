using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sessions");

            migrationBuilder.CreateTable(
                name: "swap_sessions",
                schema: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    other_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    completion_marked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completion_marked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completion_confirm_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_automatically = table.Column<bool>(type: "boolean", nullable: false),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_swap_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_swap_sessions_match_id_created_at",
                schema: "sessions",
                table: "swap_sessions",
                columns: new[] { "match_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_swap_sessions_other_user_id",
                schema: "sessions",
                table: "swap_sessions",
                column: "other_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_swap_sessions_proposed_by_user_id",
                schema: "sessions",
                table: "swap_sessions",
                column: "proposed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_swap_sessions_status_completion_confirm_deadline",
                schema: "sessions",
                table: "swap_sessions",
                columns: new[] { "status", "completion_confirm_deadline" });

            migrationBuilder.CreateIndex(
                name: "ix_swap_sessions_status_scheduled_at",
                schema: "sessions",
                table: "swap_sessions",
                columns: new[] { "status", "scheduled_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "swap_sessions",
                schema: "sessions");
        }
    }
}
