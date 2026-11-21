using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HNMO.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyDropDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "MoneyDropDuration",
                schema: "config",
                table: "GameConfiguration",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 15, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoneyDropDuration",
                schema: "config",
                table: "GameConfiguration");
        }
    }
}
