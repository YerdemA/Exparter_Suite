using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "matching");

            migrationBuilder.CreateTable(
                name: "swap_requests",
                schema: "matching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offered_skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_swap_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                schema: "matching",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    swap_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_a_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_b_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_taught_by_a_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_taught_by_b_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    end_requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    end_requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_request_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_matches_swap_requests_swap_request_id",
                        column: x => x.swap_request_id,
                        principalSchema: "matching",
                        principalTable: "swap_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_matches_status_end_request_expires_at",
                schema: "matching",
                table: "matches",
                columns: new[] { "status", "end_request_expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_matches_swap_request_id",
                schema: "matching",
                table: "matches",
                column: "swap_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_user_a_id_created_at",
                schema: "matching",
                table: "matches",
                columns: new[] { "user_a_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_matches_user_b_id_created_at",
                schema: "matching",
                table: "matches",
                columns: new[] { "user_b_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_swap_requests_recipient_id_created_at",
                schema: "matching",
                table: "swap_requests",
                columns: new[] { "recipient_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_swap_requests_requester_id_created_at",
                schema: "matching",
                table: "swap_requests",
                columns: new[] { "requester_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_swap_requests_status_expires_at",
                schema: "matching",
                table: "swap_requests",
                columns: new[] { "status", "expires_at" });

            // One pending request and one unfinished match per pair of users, whichever
            // of the two sent the request. EF cannot describe indexes over expressions.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_swap_requests_pending_pair
                    ON matching.swap_requests (LEAST(requester_id, recipient_id), GREATEST(requester_id, recipient_id))
                    WHERE status = 'Pending';
                CREATE UNIQUE INDEX ix_matches_open_pair
                    ON matching.matches (LEAST(user_a_id, user_b_id), GREATEST(user_a_id, user_b_id))
                    WHERE status <> 'Ended';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matches",
                schema: "matching");

            migrationBuilder.DropTable(
                name: "swap_requests",
                schema: "matching");
        }
    }
}
