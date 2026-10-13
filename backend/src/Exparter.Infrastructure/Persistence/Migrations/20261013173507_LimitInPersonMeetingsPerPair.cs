using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LimitInPersonMeetingsPerPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Meetings reported before the limit existed: each pair keeps its earliest
            // one, the rest are cancelled so that the index can be created.
            // The index is over the pair in either order, which EF cannot describe.
            migrationBuilder.Sql(
                """
                UPDATE sessions.swap_sessions AS later
                SET status = 'Cancelled', cancelled_at = now(), completion_confirm_deadline = NULL
                WHERE later.mode = 'InPerson'
                    AND later.status <> 'Cancelled'
                    AND EXISTS (
                        SELECT 1
                        FROM sessions.swap_sessions AS earlier
                        WHERE earlier.mode = 'InPerson'
                            AND earlier.status <> 'Cancelled'
                            AND earlier.id < later.id
                            AND LEAST(earlier.proposed_by_user_id, earlier.other_user_id)
                                = LEAST(later.proposed_by_user_id, later.other_user_id)
                            AND GREATEST(earlier.proposed_by_user_id, earlier.other_user_id)
                                = GREATEST(later.proposed_by_user_id, later.other_user_id));
                CREATE UNIQUE INDEX ix_swap_sessions_in_person_pair
                    ON sessions.swap_sessions (LEAST(proposed_by_user_id, other_user_id), GREATEST(proposed_by_user_id, other_user_id))
                    WHERE mode = 'InPerson' AND status <> 'Cancelled';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX sessions.ix_swap_sessions_in_person_pair;");
        }
    }
}
