using ExamSystem.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class CandidateExamAttemptGrantConfiguration : IEntityTypeConfiguration<CandidateExamAttemptGrant>
{
    public void Configure(EntityTypeBuilder<CandidateExamAttemptGrant> builder)
    {
        builder.HasIndex(g => new { g.CandidateId, g.ExamId });
    }
}
