using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamQueueMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QueueMode",
                table: "Exams",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QueueMode",
                table: "Exams");
        }
    }
}
