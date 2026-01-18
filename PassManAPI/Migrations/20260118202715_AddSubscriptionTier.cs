using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PassManAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MaxVaults = table.Column<int>(type: "int", nullable: false),
                    MaxCredentialsPerVault = table.Column<int>(type: "int", nullable: false),
                    MaxAttachmentSizeMB = table.Column<int>(type: "int", nullable: false),
                    AllowsSharing = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionTiers", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "SubscriptionTiers",
                columns: new[] { "Id", "AllowsSharing", "MaxAttachmentSizeMB", "MaxCredentialsPerVault", "MaxVaults", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), false, 5, 50, 3, "Free", 0m },
                    { new Guid("00000000-0000-0000-0000-000000000002"), true, 100, 1000, 50, "Premium", 9.99m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SubscriptionTierId",
                table: "AspNetUsers",
                column: "SubscriptionTierId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_SubscriptionTiers_SubscriptionTierId",
                table: "AspNetUsers",
                column: "SubscriptionTierId",
                principalTable: "SubscriptionTiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_SubscriptionTiers_SubscriptionTierId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "SubscriptionTiers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SubscriptionTierId",
                table: "AspNetUsers");
        }
    }
}
