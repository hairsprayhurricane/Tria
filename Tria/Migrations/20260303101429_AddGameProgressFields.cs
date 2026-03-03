using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tria.Migrations
{
    /// <inheritdoc />
    public partial class AddGameProgressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameScore",
                table: "UserModuleProgress",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsGameCompleted",
                table: "UserModuleProgress",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasGame",
                table: "LearningBlocks",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameScore",
                table: "UserModuleProgress");

            migrationBuilder.DropColumn(
                name: "IsGameCompleted",
                table: "UserModuleProgress");

            migrationBuilder.DropColumn(
                name: "HasGame",
                table: "LearningBlocks");
        }
    }
}
