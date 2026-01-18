using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PassManAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogVaultCredentialLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CredentialId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VaultId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CredentialId",
                table: "AuditLogs",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_VaultId",
                table: "AuditLogs",
                column: "VaultId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Credentials_CredentialId",
                table: "AuditLogs",
                column: "CredentialId",
                principalTable: "Credentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Vaults_VaultId",
                table: "AuditLogs",
                column: "VaultId",
                principalTable: "Vaults",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Credentials_CredentialId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Vaults_VaultId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CredentialId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_VaultId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CredentialId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "VaultId",
                table: "AuditLogs");
        }
    }
}
