using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNMO.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSerial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Serial",
                schema: "data",
                table: "Item",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Item_Serial",
                schema: "data",
                table: "Item",
                column: "Serial");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Item_Serial",
                schema: "data",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "Serial",
                schema: "data",
                table: "Item");
        }
    }
}
