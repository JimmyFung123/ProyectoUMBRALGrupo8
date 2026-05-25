using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClueService.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoClueData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make existing Content column nullable (TreasureHunt clues won't carry text).
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Clues",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            // New columns for TreasureHunt clues.
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Clues",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Clues",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RadiusMeters",
                table: "Clues",
                type: "integer",
                nullable: true);

            // StageType is non-null: backfill existing rows to 'Trivia' before enforcing.
            migrationBuilder.AddColumn<string>(
                name: "StageType",
                table: "Clues",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Trivia");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Latitude", table: "Clues");
            migrationBuilder.DropColumn(name: "Longitude", table: "Clues");
            migrationBuilder.DropColumn(name: "RadiusMeters", table: "Clues");
            migrationBuilder.DropColumn(name: "StageType", table: "Clues");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Clues",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
