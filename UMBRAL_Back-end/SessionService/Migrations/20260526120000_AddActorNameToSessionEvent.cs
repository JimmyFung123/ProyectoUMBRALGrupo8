using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionService.Migrations
{
    /// <inheritdoc />
    public partial class AddActorNameToSessionEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the column with a temporary default so existing rows backfill cleanly
            // ("Sistema" matches SessionEvent.SystemActor on the domain side).
            migrationBuilder.AddColumn<string>(
                name: "ActorName",
                table: "SessionEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Sistema");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorName",
                table: "SessionEvents");
        }
    }
}
