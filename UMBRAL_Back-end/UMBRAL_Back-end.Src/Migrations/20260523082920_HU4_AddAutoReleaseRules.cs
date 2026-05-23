using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMBRAL_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class HU4_AddAutoReleaseRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoReleaseMaxAttempts",
                table: "MissionStages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoReleaseTimeMinutes",
                table: "MissionStages",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoReleaseMaxAttempts",
                table: "MissionStages");

            migrationBuilder.DropColumn(
                name: "AutoReleaseTimeMinutes",
                table: "MissionStages");
        }
    }
}
