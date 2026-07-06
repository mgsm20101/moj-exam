using ExamSystem.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.Property(c => c.NationalId).IsRequired().HasMaxLength(14);
        builder.Property(c => c.FullName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.MobileNumber).IsRequired().HasMaxLength(11);
        builder.HasIndex(c => c.NationalId).IsUnique();

        builder.HasMany(c => c.Grants)
            .WithOne(g => g.Candidate!)
            .HasForeignKey(g => g.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
