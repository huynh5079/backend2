using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class Add_Entity_RescheduleRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RescheduleRequest",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequesterUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LessonId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OriginalScheduleEntryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OldStartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldEndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewStartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewEndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResponderUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RescheduleRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RescheduleRequest_Lesson_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lesson",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RescheduleRequest_ScheduleEntry_OriginalScheduleEntryId",
                        column: x => x.OriginalScheduleEntryId,
                        principalTable: "ScheduleEntry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RescheduleRequest_User_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RescheduleRequest_User_ResponderUserId",
                        column: x => x.ResponderUserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleRequest_LessonId",
                table: "RescheduleRequest",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleRequest_OriginalScheduleEntryId",
                table: "RescheduleRequest",
                column: "OriginalScheduleEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleRequest_RequesterUserId",
                table: "RescheduleRequest",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleRequest_ResponderUserId",
                table: "RescheduleRequest",
                column: "ResponderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RescheduleRequest");
        }
    }
}
