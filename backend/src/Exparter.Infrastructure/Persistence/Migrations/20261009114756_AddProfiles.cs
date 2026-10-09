using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "profiles");

            migrationBuilder.CreateTable(
                name: "profiles",
                schema: "profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    avatar_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    city = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    accepts_online = table.Column<bool>(type: "boolean", nullable: false),
                    accepts_in_person = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profiles", x => x.user_id);
                });

            // Accounts created before this migration never went through the registration
            // event that creates a profile, so they get their blank profile here.
            migrationBuilder.Sql(
                """
                INSERT INTO profiles.profiles (user_id, accepts_online, accepts_in_person, created_at, updated_at)
                SELECT id, FALSE, FALSE, created_at, created_at FROM identity.users;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profiles",
                schema: "profiles");
        }
    }
}
