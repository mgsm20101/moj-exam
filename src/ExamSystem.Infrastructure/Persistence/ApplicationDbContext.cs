using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExamSystem.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamTopicSelection> ExamTopicSelections => Set<ExamTopicSelection>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // SQL Server's datetime2 columns don't preserve DateTimeKind, so every DateTime read back
        // from the database comes back as Kind=Unspecified. System.Text.Json only appends the "Z"
        // UTC designator when Kind=Utc, so without this the API serializes timestamps like
        // "2026-07-10T06:00:00" (no "Z"), and the browser's `new Date(...)` then misparses them as
        // local time instead of UTC -- silently shifting exam start/end times by the browser's UTC
        // offset on every round trip through an edit form. All our DateTime columns are UTC by
        // convention (Utc suffix in the property name), so force Kind=Utc on every read.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}

file class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
