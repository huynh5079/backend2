using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Commission",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OneToOneOnline = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    OneToOneOffline = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    GroupClassOnline = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    GroupClassOffline = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PerLesson = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    FullCourse = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commission", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Commission");
        }
    }
}
