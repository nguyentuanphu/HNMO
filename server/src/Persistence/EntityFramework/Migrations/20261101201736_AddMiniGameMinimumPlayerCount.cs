using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNMO.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMiniGameMinimumPlayerCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumPlayerCount",
                schema: "config",
                table: "MiniGameDefinition",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumPlayerCount",
                schema: "config",
                table: "MiniGameDefinition");
        }
    }
}
