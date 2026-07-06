using ExamSystem.Domain.Attempts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class AttemptAnswerConfiguration : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.Property(a => a.AnswerText).HasMaxLength(50);
        builder.HasIndex(a => new { a.AttemptId, a.AttemptQuestionId }).IsUnique();

        builder.HasOne<ExamAttempt>()
            .WithMany(e => e.Answers)
            .HasForeignKey(a => a.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
