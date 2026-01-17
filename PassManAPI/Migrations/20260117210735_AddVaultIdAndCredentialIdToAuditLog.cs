using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PassManAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultIdAndCredentialIdToAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Vaults",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Credentials",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "AuditLogs",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Vaults",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Credentials",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "AuditLogs",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");
        }
    }
}
