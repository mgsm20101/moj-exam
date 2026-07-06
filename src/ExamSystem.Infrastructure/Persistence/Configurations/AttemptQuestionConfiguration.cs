using ExamSystem.Domain.Attempts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class AttemptQuestionConfiguration : IEntityTypeConfiguration<AttemptQuestion>
{
    public void Configure(EntityTypeBuilder<AttemptQuestion> builder)
    {
        builder.Property(q => q.TextSnapshot).IsRequired();
        builder.Property(q => q.CorrectAnswerTextSnapshot).HasMaxLength(50);
        builder.HasIndex(q => new { q.AttemptId, q.DisplayOrder });

        builder.HasMany(q => q.Options)
            .WithOne()
            .HasForeignKey(o => o.AttemptQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
