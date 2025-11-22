using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class Add_Entity_In_Feedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassId",
                table: "Feedback",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedback_ClassId",
                table: "Feedback",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "UQ_Feedback_From_To_Class",
                table: "Feedback",
                columns: new[] { "FromUserId", "ToUserId", "ClassId" },
                unique: true,
                filter: "[FromUserId] IS NOT NULL AND [ToUserId] IS NOT NULL AND [ClassId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK__Feedback__Class__261B255A",
                table: "Feedback",
                column: "ClassId",
                principalTable: "Class",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Feedback__Class__261B255A",
                table: "Feedback");

            migrationBuilder.DropIndex(
                name: "IX_Feedback_ClassId",
                table: "Feedback");

            migrationBuilder.DropIndex(
                name: "UQ_Feedback_From_To_Class",
                table: "Feedback");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "Feedback");
        }
    }
}
