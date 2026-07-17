using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionService.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionAccessCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessCode",
                table: "Sessions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            // Assign a unique code to every pre-existing row before creating the unique index.
            // UPPER(LEFT(MD5(id::text), 6)) produces a deterministic, unique 6-char hex string per row.
            migrationBuilder.Sql(
                "UPDATE \"Sessions\" SET \"AccessCode\" = UPPER(LEFT(MD5(\"Id\"::text), 6)) WHERE \"AccessCode\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_AccessCode",
                table: "Sessions",
                column: "AccessCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_AccessCode",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "AccessCode",
                table: "Sessions");
        }
    }
}
