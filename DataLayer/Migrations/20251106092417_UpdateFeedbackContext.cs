using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFeedbackContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UQ_Feedback_From_To_Lesson",
                table: "Feedback",
                columns: new[] { "FromUserId", "ToUserId", "LessonId" },
                unique: true,
                filter: "[FromUserId] IS NOT NULL AND [ToUserId] IS NOT NULL AND [LessonId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Feedback_From_To_Lesson",
                table: "Feedback");
        }
    }
}
