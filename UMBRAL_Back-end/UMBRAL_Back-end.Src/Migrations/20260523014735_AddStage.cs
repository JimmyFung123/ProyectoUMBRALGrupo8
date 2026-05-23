using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMBRAL_Back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "MissionStages",
                newName: "Title");

            migrationBuilder.AddColumn<int>(
                name: "BaseScore",
                table: "MissionStages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "MissionStages",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "MissionStages",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "MissionStages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Question",
                table: "MissionStages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "MissionStages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TriviaOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriviaOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriviaOptions_MissionStages_StageId",
                        column: x => x.StageId,
                        principalTable: "MissionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissionStages_QrCode",
                table: "MissionStages",
                column: "QrCode",
                unique: true,
                filter: "\"QrCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TriviaOptions_StageId",
                table: "TriviaOptions",
                column: "StageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriviaOptions");

            migrationBuilder.DropIndex(
                name: "IX_MissionStages_QrCode",
                table: "MissionStages");

            migrationBuilder.DropColumn(
                name: "BaseScore",
                table: "MissionStages");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "MissionStages");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "MissionStages");

            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "MissionStages");

            migrationBuilder.DropColumn(
                name: "Question",
                table: "MissionStages");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "MissionStages");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "MissionStages",
                newName: "Name");
        }
    }
}
