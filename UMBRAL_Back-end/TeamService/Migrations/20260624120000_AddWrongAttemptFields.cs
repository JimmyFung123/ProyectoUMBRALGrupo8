using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamService.Migrations
{
    /// <inheritdoc />
    public partial class AddWrongAttemptFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WrongAttemptsCurrentStage",
                table: "Teams",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BlockedOptionIds",
                table: "Teams",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WrongAttemptsCurrentStage",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "BlockedOptionIds",
                table: "Teams");
        }
    }
}
