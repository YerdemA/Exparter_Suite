using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exparter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndexPendingDeletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_users_deletion_requested_at",
                schema: "identity",
                table: "users",
                column: "deletion_requested_at",
                filter: "status = 'PendingDeletion'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_deletion_requested_at",
                schema: "identity",
                table: "users");
        }
    }
}
