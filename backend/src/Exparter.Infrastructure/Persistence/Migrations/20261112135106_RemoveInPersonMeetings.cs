using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInPersonMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // In-person meetings are gone from the product, and so are the ones on record.
            // A review names the completed session that allowed it: where the two users
            // also completed an online session the review moves to that one, otherwise
            // it had no other ground and goes with the meeting.
            migrationBuilder.Sql(
                """
                UPDATE reviews.reviews AS r
                SET swap_session_id = online.id
                FROM sessions.swap_sessions AS met
                CROSS JOIN LATERAL (
                    SELECT s.id
                    FROM sessions.swap_sessions AS s
                    WHERE s.mode = 'Online'
                        AND (s.status = 'Completed'
                            OR (s.status = 'CompletionPending' AND s.completion_confirm_deadline <= now()))
                        AND LEAST(s.proposed_by_user_id, s.other_user_id) = LEAST(met.proposed_by_user_id, met.other_user_id)
                        AND GREATEST(s.proposed_by_user_id, s.other_user_id) = GREATEST(met.proposed_by_user_id, met.other_user_id)
                    ORDER BY s.id
                    LIMIT 1
                ) AS online
                WHERE met.id = r.swap_session_id AND met.mode = 'InPerson';

                DELETE FROM reviews.reviews AS r
                USING sessions.swap_sessions AS met
                WHERE met.id = r.swap_session_id AND met.mode = 'InPerson';

                DELETE FROM sessions.swap_sessions WHERE mode = 'InPerson';

                DROP INDEX sessions.ix_swap_sessions_in_person_pair;
                """);

            migrationBuilder.DropColumn(
                name: "mode",
                schema: "sessions",
                table: "swap_sessions");

            migrationBuilder.DropColumn(
                name: "accepts_in_person",
                schema: "profiles",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "accepts_online",
                schema: "profiles",
                table: "profiles");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "scheduled_at",
                schema: "sessions",
                table: "swap_sessions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "duration_minutes",
                schema: "sessions",
                table: "swap_sessions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "scheduled_at",
                schema: "sessions",
                table: "swap_sessions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "duration_minutes",
                schema: "sessions",
                table: "swap_sessions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "mode",
                schema: "sessions",
                table: "swap_sessions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Online");

            migrationBuilder.AddColumn<bool>(
                name: "accepts_in_person",
                schema: "profiles",
                table: "profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "accepts_online",
                schema: "profiles",
                table: "profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_swap_sessions_in_person_pair
                    ON sessions.swap_sessions (LEAST(proposed_by_user_id, other_user_id), GREATEST(proposed_by_user_id, other_user_id))
                    WHERE mode = 'InPerson' AND status <> 'Cancelled';
                """);
        }
    }
}
