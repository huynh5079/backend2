using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemovePerLessonAndFullCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullCourse",
                table: "Commission");

            migrationBuilder.DropColumn(
                name: "PerLesson",
                table: "Commission");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FullCourse",
                table: "Commission",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PerLesson",
                table: "Commission",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
