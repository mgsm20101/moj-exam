using ExamSystem.Domain.Attempts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.Property(a => a.Score).HasColumnType("decimal(6,2)");
        builder.HasIndex(a => new { a.ExamId, a.CandidateId });

        builder.HasMany(a => a.Questions)
            .WithOne()
            .HasForeignKey(q => q.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
