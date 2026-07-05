using ExamSystem.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.McqPoints).HasColumnType("decimal(5,2)");
        builder.Property(e => e.TrueFalsePoints).HasColumnType("decimal(5,2)");
        builder.Property(e => e.FillBlankPoints).HasColumnType("decimal(5,2)");
        builder.Property(e => e.PassMarkPercentage).HasColumnType("decimal(5,2)");

        builder.HasIndex(e => e.Status);
    }
}
