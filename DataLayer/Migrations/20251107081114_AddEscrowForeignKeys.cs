using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddEscrowForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Escrow_PayerUserId",
                table: "Escrow",
                column: "PayerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Escrow_TutorUserId",
                table: "Escrow",
                column: "TutorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Escrow_Class",
                table: "Escrow",
                column: "ClassId",
                principalTable: "Class",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Escrow_PayerUser",
                table: "Escrow",
                column: "PayerUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Escrow_TutorUser",
                table: "Escrow",
                column: "TutorUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Escrow_Class",
                table: "Escrow");

            migrationBuilder.DropForeignKey(
                name: "FK_Escrow_PayerUser",
                table: "Escrow");

            migrationBuilder.DropForeignKey(
                name: "FK_Escrow_TutorUser",
                table: "Escrow");

            migrationBuilder.DropIndex(
                name: "IX_Escrow_PayerUserId",
                table: "Escrow");

            migrationBuilder.DropIndex(
                name: "IX_Escrow_TutorUserId",
                table: "Escrow");
        }
    }
}
