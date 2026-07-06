using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTabSwitchCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TabSwitchCount",
                table: "ExamAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TabSwitchCount",
                table: "ExamAttempts");
        }
    }
}
