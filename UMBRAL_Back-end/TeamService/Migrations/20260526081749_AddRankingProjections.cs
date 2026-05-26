using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamService.Migrations
{
    /// <inheritdoc />
    public partial class AddRankingProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RankingProjections",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CurrentStageOrder = table.Column<int>(type: "integer", nullable: false),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false),
                    LastStageCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingProjections", x => new { x.SessionId, x.TeamId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_RankingProjections_SessionId_Position",
                table: "RankingProjections",
                columns: new[] { "SessionId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankingProjections");
        }
    }
}
