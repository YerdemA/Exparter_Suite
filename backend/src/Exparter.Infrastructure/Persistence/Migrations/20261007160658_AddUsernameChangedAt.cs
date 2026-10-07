using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameChangedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "username_changed_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "username_changed_at",
                schema: "identity",
                table: "users");
        }
    }
}
