using ExamSystem.Domain.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class WaitingQueueEntryConfiguration : IEntityTypeConfiguration<WaitingQueueEntry>
{
    public void Configure(EntityTypeBuilder<WaitingQueueEntry> builder)
    {
        builder.HasIndex(e => new { e.ExamId, e.Status, e.EnqueuedAtUtc });
        builder.HasIndex(e => new { e.ExamId, e.CandidateId });
    }
}
