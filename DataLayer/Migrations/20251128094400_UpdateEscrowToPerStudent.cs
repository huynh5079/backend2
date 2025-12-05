using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEscrowToPerStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Escrow_Class_ClassId",
                table: "Escrow");

            migrationBuilder.DropForeignKey(
                name: "FK_Escrow_PayerUser",
                table: "Escrow");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "Escrow");

            migrationBuilder.RenameColumn(
                name: "PayerUserId",
                table: "Escrow",
                newName: "StudentUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Escrow_PayerUserId",
                table: "Escrow",
                newName: "IX_Escrow_StudentUserId");

            migrationBuilder.AlterColumn<string>(
                name: "EscrowId",
                table: "TutorDepositEscrow",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            // Xóa các Escrow có TutorUserId = NULL (dữ liệu cũ không hợp lệ)
            migrationBuilder.Sql(@"
                DELETE FROM [Escrow] WHERE [TutorUserId] IS NULL;
            ");

            // Sau đó mới alter column thành NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "TutorUserId",
                table: "Escrow",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            // Xóa tất cả Escrow cũ vì cấu trúc mới khác hoàn toàn (mỗi học sinh = 1 escrow)
            // Dữ liệu cũ không thể migrate tự động
            migrationBuilder.Sql(@"
                DELETE FROM [Escrow];
            ");

            migrationBuilder.AddColumn<string>(
                name: "ClassAssignId",
                table: "Escrow",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRateSnapshot",
                table: "Escrow",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Escrow",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReleasedAmount",
                table: "Escrow",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Escrow_ClassAssignId",
                table: "Escrow",
                column: "ClassAssignId");

            migrationBuilder.AddForeignKey(
                name: "FK_Escrow_Class",
                table: "Escrow",
                column: "ClassId",
                principalTable: "Class",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Escrow_ClassAssign",
                table: "Escrow",
                column: "ClassAssignId",
                principalTable: "ClassAssign",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Escrow_StudentUser",
                table: "Escrow",
                column: "StudentUserId",
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
                name: "FK_Escrow_ClassAssign",
                table: "Escrow");

            migrationBuilder.DropForeignKey(
                name: "FK_Escrow_StudentUser",
                table: "Escrow");

            migrationBuilder.DropIndex(
                name: "IX_Escrow_ClassAssignId",
                table: "Escrow");

            migrationBuilder.DropColumn(
                name: "ClassAssignId",
                table: "Escrow");

            migrationBuilder.DropColumn(
                name: "CommissionRateSnapshot",
                table: "Escrow");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Escrow");

            migrationBuilder.DropColumn(
                name: "ReleasedAmount",
                table: "Escrow");

            migrationBuilder.RenameColumn(
                name: "StudentUserId",
                table: "Escrow",
                newName: "PayerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Escrow_StudentUserId",
                table: "Escrow",
                newName: "IX_Escrow_PayerUserId");

            migrationBuilder.AlterColumn<string>(
                name: "EscrowId",
                table: "TutorDepositEscrow",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TutorUserId",
                table: "Escrow",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "Escrow",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_Escrow_Class_ClassId",
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
        }
    }
}
