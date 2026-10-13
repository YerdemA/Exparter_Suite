using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_low_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_high_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_low_last_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    user_high_last_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    client_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "messaging",
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_pair",
                schema: "messaging",
                table: "conversations",
                columns: new[] { "user_low_id", "user_high_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversations_user_high_id_last_activity_at",
                schema: "messaging",
                table: "conversations",
                columns: new[] { "user_high_id", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_user_low_id_last_activity_at",
                schema: "messaging",
                table: "conversations",
                columns: new[] { "user_low_id", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_client_id",
                schema: "messaging",
                table: "messages",
                columns: new[] { "conversation_id", "sender_id", "client_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_id_sent_at_id",
                schema: "messaging",
                table: "messages",
                columns: new[] { "conversation_id", "sent_at", "id" });

            // Matches made before conversations existed get theirs now: one per pair of users.
            migrationBuilder.Sql(
                """
                INSERT INTO messaging.conversations (id, user_low_id, user_high_id, created_at, last_activity_at)
                SELECT gen_random_uuid(), pair.low, pair.high, pair.first_matched_at, pair.first_matched_at
                FROM (
                    SELECT LEAST(user_a_id, user_b_id) AS low, GREATEST(user_a_id, user_b_id) AS high,
                           MIN(created_at) AS first_matched_at
                    FROM matching.matches
                    GROUP BY 1, 2
                ) pair;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "messaging");
        }
    }
}
