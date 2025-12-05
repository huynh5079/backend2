using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSystemSettingsDepositCalculation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DefaultDepositAmount",
                table: "SystemSettings",
                newName: "MinDepositAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "DepositRate",
                table: "SystemSettings",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDepositAmount",
                table: "SystemSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositRate",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "MaxDepositAmount",
                table: "SystemSettings");

            migrationBuilder.RenameColumn(
                name: "MinDepositAmount",
                table: "SystemSettings",
                newName: "DefaultDepositAmount");
        }
    }
}
