using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveApplicationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK__TutorApp__C93A4C990407478F",
                table: "TutorApplication");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "TutorApplication");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "TutorApplication",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TutorApplication",
                table: "TutorApplication",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TutorApplication",
                table: "TutorApplication");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "TutorApplication",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationId",
                table: "TutorApplication",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK__TutorApp__C93A4C990407478F",
                table: "TutorApplication",
                column: "ApplicationId");
        }
    }
}
