using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionService.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandMetadataToSessionEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HU-26: enrich SessionEvent with the technical metadata expected
            // from a CQRS-style command audit log. Both columns are nullable so
            // legacy rows (HU-22) don't need a backfill.
            migrationBuilder.AddColumn<string>(
                name: "CommandType",
                table: "SessionEvents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "SessionEvents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandType",
                table: "SessionEvents");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "SessionEvents");
        }
    }
}
