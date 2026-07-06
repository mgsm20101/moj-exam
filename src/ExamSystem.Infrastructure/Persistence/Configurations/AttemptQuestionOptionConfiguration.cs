using ExamSystem.Domain.Attempts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class AttemptQuestionOptionConfiguration : IEntityTypeConfiguration<AttemptQuestionOption>
{
    public void Configure(EntityTypeBuilder<AttemptQuestionOption> builder)
    {
        builder.Property(o => o.TextSnapshot).IsRequired();
    }
}
