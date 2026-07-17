using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SessionService.Migrations
{
    /// <inheritdoc />
    public partial class AddStageCompletionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StageCompletionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageOrder = table.Column<int>(type: "integer", nullable: false),
                    StageType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ElapsedSeconds = table.Column<int>(type: "integer", nullable: false),
                    WasCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    WasForceAdvance = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IncludedInStatistics = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageCompletionRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageCompletionRecords_IncludedInStatistics_MissionId_Stage~",
                table: "StageCompletionRecords",
                columns: new[] { "IncludedInStatistics", "MissionId", "StageOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StageCompletionRecords_SessionId",
                table: "StageCompletionRecords",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StageCompletionRecords");
        }
    }
}
