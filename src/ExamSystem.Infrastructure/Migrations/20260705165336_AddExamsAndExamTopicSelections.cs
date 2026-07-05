using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExamsAndExamTopicSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Exams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    McqPoints = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TrueFalsePoints = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    FillBlankPoints = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PassMarkPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    ShuffleAnswers = table.Column<bool>(type: "bit", nullable: false),
                    ShowResultImmediately = table.Column<bool>(type: "bit", nullable: false),
                    AllowBackNavigation = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamTopicSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamTopicSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamTopicSelections_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamTopicSelections_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Status",
                table: "Exams",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ExamTopicSelections_ExamId_TopicId_Difficulty_Type",
                table: "ExamTopicSelections",
                columns: new[] { "ExamId", "TopicId", "Difficulty", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamTopicSelections_TopicId",
                table: "ExamTopicSelections",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamTopicSelections");

            migrationBuilder.DropTable(
                name: "Exams");
        }
    }
}
