using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNMO.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPentagramToItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "PentagramElement",
                schema: "data",
                table: "Item",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "PentagramErrtels",
                schema: "data",
                table: "Item",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PentagramElement",
                schema: "data",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "PentagramErrtels",
                schema: "data",
                table: "Item");
        }
    }
}
