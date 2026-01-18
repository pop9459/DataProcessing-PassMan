using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PassManAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultIconAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: Icon and IsDeleted columns were introduced in an earlier migration
            // (20260117205357_AddIconAndIsDeletedToVault). Keeping this migration empty
            // prevents duplicate column errors when applying the migration set to a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: see Up() comment.
        }
    }
}
