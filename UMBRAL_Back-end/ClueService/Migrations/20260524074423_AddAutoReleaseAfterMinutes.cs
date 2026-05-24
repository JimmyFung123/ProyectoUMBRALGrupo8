using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClueService.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoReleaseAfterMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoReleaseAfterMinutes",
                table: "Clues",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoReleaseAfterMinutes",
                table: "Clues");
        }
    }
}
