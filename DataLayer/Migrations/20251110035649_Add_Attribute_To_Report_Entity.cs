using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class Add_Attribute_To_Report_Entity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Report",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetMediaId",
                table: "Report",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Report_TargetMediaId",
                table: "Report",
                column: "TargetMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Report_TargetMedia",
                table: "Report",
                column: "TargetMediaId",
                principalTable: "Media",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Report_TargetMedia",
                table: "Report");

            migrationBuilder.DropIndex(
                name: "IX_Report_TargetMediaId",
                table: "Report");

            migrationBuilder.DropColumn(
                name: "TargetMediaId",
                table: "Report");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Report",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);
        }
    }
}
