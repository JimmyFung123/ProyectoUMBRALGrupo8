using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionService.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedByOperatorIdToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByOperatorId",
                table: "Sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CreatedByOperatorId",
                table: "Sessions",
                column: "CreatedByOperatorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_CreatedByOperatorId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CreatedByOperatorId",
                table: "Sessions");
        }
    }
}
