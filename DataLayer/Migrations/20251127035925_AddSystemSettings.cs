using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DefaultDepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TutorDepositEscrow",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClassId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EscrowId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TutorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RefundedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ForfeitedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ForfeitReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorDepositEscrow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorDepositEscrow_Class",
                        column: x => x.ClassId,
                        principalTable: "Class",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorDepositEscrow_Escrow",
                        column: x => x.EscrowId,
                        principalTable: "Escrow",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorDepositEscrow_TutorUser",
                        column: x => x.TutorUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorDepositEscrow_ClassId",
                table: "TutorDepositEscrow",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDepositEscrow_EscrowId",
                table: "TutorDepositEscrow",
                column: "EscrowId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDepositEscrow_TutorUserId",
                table: "TutorDepositEscrow",
                column: "TutorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TutorDepositEscrow");
        }
    }
}
