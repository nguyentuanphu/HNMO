using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNMO.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddJewelBankBalancesAndAssetOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetOperation",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    PendingKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PendingCount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetOperation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetOperation_Account_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "data",
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JewelBankBalance",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    JewelKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JewelBankBalance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JewelBankBalance_Account_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "data",
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetOperation_AccountId_PendingCount",
                schema: "data",
                table: "AssetOperation",
                columns: new[] { "AccountId", "PendingCount" });

            migrationBuilder.CreateIndex(
                name: "IX_JewelBankBalance_AccountId_JewelKey",
                schema: "data",
                table: "JewelBankBalance",
                columns: new[] { "AccountId", "JewelKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetOperation",
                schema: "data");

            migrationBuilder.DropTable(
                name: "JewelBankBalance",
                schema: "data");
        }
    }
}
