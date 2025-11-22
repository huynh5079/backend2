using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFKRestrict_Subject_EducationLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Class__Education__09A971A2",
                table: "Class");

            migrationBuilder.DropForeignKey(
                name: "FK__Class__SubjectId__08B54D69",
                table: "Class");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassRequest_EducationLevel_EducationLevelId",
                table: "ClassRequest");

            migrationBuilder.DropForeignKey(
                name: "FK__ClassRequ__Subje__0C85DE4D",
                table: "ClassRequest");

            migrationBuilder.DropForeignKey(
                name: "FK__StudentPr__Educa__03F0984C",
                table: "StudentProfile");

            migrationBuilder.AddForeignKey(
                name: "FK__Class__Education__09A971A2",
                table: "Class",
                column: "EducationLevelId",
                principalTable: "EducationLevel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK__Class__SubjectId__08B54D69",
                table: "Class",
                column: "SubjectId",
                principalTable: "Subject",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassRequest_EducationLevel_EducationLevelId",
                table: "ClassRequest",
                column: "EducationLevelId",
                principalTable: "EducationLevel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK__ClassRequ__Subje__0C85DE4D",
                table: "ClassRequest",
                column: "SubjectId",
                principalTable: "Subject",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK__StudentPr__Educa__03F0984C",
                table: "StudentProfile",
                column: "EducationLevelId",
                principalTable: "EducationLevel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Class__Education__09A971A2",
                table: "Class");

            migrationBuilder.DropForeignKey(
                name: "FK__Class__SubjectId__08B54D69",
                table: "Class");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassRequest_EducationLevel_EducationLevelId",
                table: "ClassRequest");

            migrationBuilder.DropForeignKey(
                name: "FK__ClassRequ__Subje__0C85DE4D",
                table: "ClassRequest");

            migrationBuilder.DropForeignKey(
                name: "FK__StudentPr__Educa__03F0984C",
                table: "StudentProfile");

            migrationBuilder.AddForeignKey(
                name: "FK__Class__Education__09A971A2",
                table: "Class",
                column: "EducationLevelId",
                principalTable: "EducationLevel",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__Class__SubjectId__08B54D69",
                table: "Class",
                column: "SubjectId",
                principalTable: "Subject",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassRequest_EducationLevel_EducationLevelId",
                table: "ClassRequest",
                column: "EducationLevelId",
                principalTable: "EducationLevel",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__ClassRequ__Subje__0C85DE4D",
                table: "ClassRequest",
                column: "SubjectId",
                principalTable: "Subject",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK__StudentPr__Educa__03F0984C",
                table: "StudentProfile",
                column: "EducationLevelId",
                principalTable: "EducationLevel",
                principalColumn: "Id");
        }
    }
}
