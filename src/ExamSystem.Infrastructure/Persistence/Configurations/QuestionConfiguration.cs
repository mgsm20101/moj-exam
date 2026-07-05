using ExamSystem.Domain.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(q => q.Text).IsRequired();
        builder.Property(q => q.CorrectAnswerText).HasMaxLength(50);
        builder.Property(q => q.PointsOverride).HasColumnType("decimal(5,2)");

        builder.HasOne(q => q.Topic)
            .WithMany(t => t.Questions)
            .HasForeignKey(q => q.TopicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(q => new { q.TopicId, q.Difficulty, q.IsActive });
    }
}
