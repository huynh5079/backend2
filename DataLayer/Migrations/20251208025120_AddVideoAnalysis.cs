using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoAnalysis",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MediaId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LessonId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Transcription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TranscriptionLanguage = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SummaryType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    KeyPoints = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VideoDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAnalysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoAnalysis_Lesson",
                        column: x => x.LessonId,
                        principalTable: "Lesson",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoAnalysis_Media",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalysis_LessonId",
                table: "VideoAnalysis",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalysis_MediaId",
                table: "VideoAnalysis",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "UQ_VideoAnalysis_MediaId",
                table: "VideoAnalysis",
                column: "MediaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoAnalysis");
        }
    }
}
