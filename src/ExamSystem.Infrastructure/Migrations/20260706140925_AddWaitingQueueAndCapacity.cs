using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitingQueueAndCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GraceWindowMinutes",
                table: "Exams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxConcurrentAttempts",
                table: "Exams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WaitingQueueEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnqueuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    CalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitingQueueEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingQueueEntries_ExamId_CandidateId",
                table: "WaitingQueueEntries",
                columns: new[] { "ExamId", "CandidateId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitingQueueEntries_ExamId_Status_EnqueuedAtUtc",
                table: "WaitingQueueEntries",
                columns: new[] { "ExamId", "Status", "EnqueuedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitingQueueEntries");

            migrationBuilder.DropColumn(
                name: "GraceWindowMinutes",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "MaxConcurrentAttempts",
                table: "Exams");
        }
    }
}
