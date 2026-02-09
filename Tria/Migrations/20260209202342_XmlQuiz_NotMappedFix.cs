using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tria.Migrations
{
    /// <inheritdoc />
    public partial class XmlQuiz_NotMappedFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "LearningBlocks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Key",
                table: "LearningBlocks");
        }
    }
}
