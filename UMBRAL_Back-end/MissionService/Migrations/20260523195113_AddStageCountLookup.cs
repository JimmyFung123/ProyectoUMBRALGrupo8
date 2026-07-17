using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMBRAL_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddStageCountLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StageCountLookup",
                columns: table => new
                {
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageCountLookup", x => x.MissionId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StageCountLookup");
        }
    }
}
