using ExamSystem.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class ExamTopicSelectionConfiguration : IEntityTypeConfiguration<ExamTopicSelection>
{
    public void Configure(EntityTypeBuilder<ExamTopicSelection> builder)
    {
        builder.HasOne(s => s.Exam)
            .WithMany(e => e.TopicSelections)
            .HasForeignKey(s => s.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Topic)
            .WithMany()
            .HasForeignKey(s => s.TopicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.ExamId, s.TopicId, s.Difficulty, s.Type }).IsUnique();
    }
}
