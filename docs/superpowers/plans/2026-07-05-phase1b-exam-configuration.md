# Phase 1b — Exam Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Admin full CRUD over Exams (FR-4.1/4.2/4.5/4.6/4.7), a Topic×Difficulty×Type question-count matrix per exam (FR-4.3, general enough to express the FR-4.11 standard 25 MCQ + 5 FillBlank / Medium+Hard distribution but not hard-coded to it), a per-type grading configuration (FR-4.4/4.12), and a Draft → Published → Closed → Archived lifecycle (FR-4.8) with Publish-time validation against the live question bank (FR-4.9).

**Architecture:** Extends the Phase 1a Clean Architecture skeleton. Adds one Domain aggregate (`Exam` + child `ExamTopicSelection`) with an `ExamStatus` enum, CQRS commands/queries under `Application/Features/Exams`, EF Core configurations + migration, and an `ExamsController`. Frontend adds a lazy-loaded `features/admin/exams` area with a matrix-style reactive form.

**Tech Stack:** .NET 8, EF Core 8 (SQL Server), MediatR, FluentValidation, xUnit + EF Core InMemory (unit tests) + SQLite (integration tests), Angular 17 standalone components + Reactive Forms + signals.

**Key design decision (confirmed with the user):** Grading is configured **per QuestionType** on the Exam (`McqPoints`, `TrueFalsePoints`, `FillBlankPoints`), not per DifficultyLevel. This exactly matches the FR-4.12 standard default (MCQ = 2, FillBlank = 5, difficulty-independent) and keeps the schema simple. Per-question `PointsOverride` (already on `Question` from Phase 1a) remains the escape hatch for the rare exception FR-4.4's "per difficulty" wording was gesturing at.

**Scope note:** This plan is FR-4 only. It does **not** cover candidate-facing exam-taking (Attempt/Snapshot), Live Monitoring (FR-5), Reporting (FR-6), or Batch Gate (FR-8) — those are later phases and depend on this one. "Active" status from FR-4.8 is **not** a stored value: it is a computed/display-only state (`Published` + `now` within `[StartAtUtc, EndAtUtc]`), avoiding the need for a background job in this phase.

---

## Prerequisites (verify before Task 1)

- [ ] **Step 1: Confirm Phase 1a is merged and the solution builds**

Run: `dotnet build` from `D:/os/ExamSystem`
Expected: `Build succeeded.`

- [ ] **Step 2: Confirm at least one Topic exists for manual testing later**

Run: `dotnet test tests/ExamSystem.Application.UnitTests` and `dotnet test tests/ExamSystem.Api.IntegrationTests`
Expected: all existing tests pass (baseline before starting Phase 1b changes).

---

## File Structure (target state after this plan)

```
D:/os/ExamSystem/
├─ src/
│  ├─ ExamSystem.Domain/
│  │  └─ Exams/
│  │     ├─ ExamStatus.cs
│  │     ├─ Exam.cs
│  │     └─ ExamTopicSelection.cs
│  ├─ ExamSystem.Application/
│  │  ├─ Common/Interfaces/IApplicationDbContext.cs      (Modify: add Exams, ExamTopicSelections)
│  │  └─ Features/Exams/
│  │     ├─ ExamTopicSelectionInput.cs
│  │     ├─ CreateExam/{CreateExamCommand,CreateExamCommandValidator,CreateExamCommandHandler}.cs
│  │     ├─ UpdateExam/{UpdateExamCommand,UpdateExamCommandValidator,UpdateExamCommandHandler}.cs
│  │     ├─ DeleteExam/{DeleteExamCommand,DeleteExamCommandHandler}.cs
│  │     ├─ GetExams/{GetExamsQuery,ExamSummaryDto,GetExamsQueryHandler}.cs
│  │     ├─ GetExamById/{GetExamByIdQuery,ExamDetailDto,ExamTopicSelectionDto,GetExamByIdQueryHandler}.cs
│  │     ├─ PublishExam/{PublishExamCommand,PublishExamCommandHandler}.cs
│  │     ├─ CloseExam/{CloseExamCommand,CloseExamCommandHandler}.cs
│  │     └─ ArchiveExam/{ArchiveExamCommand,ArchiveExamCommandHandler}.cs
│  ├─ ExamSystem.Infrastructure/
│  │  ├─ Persistence/
│  │  │  ├─ ApplicationDbContext.cs                       (Modify: add DbSets)
│  │  │  └─ Configurations/{ExamConfiguration,ExamTopicSelectionConfiguration}.cs
│  │  └─ Migrations/*                                      (generated)
│  └─ ExamSystem.Api/
│     └─ Controllers/ExamsController.cs
├─ tests/
│  ├─ ExamSystem.Application.UnitTests/Features/Exams/*Tests.cs
│  └─ ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs
└─ frontend/src/app/
   ├─ core/services/exam.service.ts
   ├─ features/admin/exams/
   │  ├─ exam-form.component.{ts,html}
   │  └─ exams-list.component.{ts,html}
   └─ app.routes.ts                                        (Modify: add 'exams' route)
```

---

### Task 1: Domain — Exam, ExamStatus, ExamTopicSelection

**Files:**
- Create: `src/ExamSystem.Domain/Exams/ExamStatus.cs`
- Create: `src/ExamSystem.Domain/Exams/Exam.cs`
- Create: `src/ExamSystem.Domain/Exams/ExamTopicSelection.cs`
- Modify: `src/ExamSystem.Domain/GlobalUsings.cs`

- [ ] **Step 1: Write `ExamStatus`**

```csharp
namespace ExamSystem.Domain.Exams;

public enum ExamStatus
{
    Draft = 0,
    Published = 1,
    Closed = 2,
    Archived = 3
}
```

- [ ] **Step 2: Write `Exam`**

```csharp
namespace ExamSystem.Domain.Exams;

public class Exam : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>Per-QuestionType grading (FR-4.4/4.12). A question's own PointsOverride wins when set.</summary>
    public decimal McqPoints { get; set; } = 2m;
    public decimal TrueFalsePoints { get; set; } = 1m;
    public decimal FillBlankPoints { get; set; } = 5m;

    public decimal PassMarkPercentage { get; set; } = 60m;
    public int MaxAttempts { get; set; } = 1;

    public bool ShuffleAnswers { get; set; } = true;
    public bool ShowResultImmediately { get; set; } = true;
    public bool AllowBackNavigation { get; set; } = true;

    public ExamStatus Status { get; set; } = ExamStatus.Draft;

    public ICollection<ExamTopicSelection> TopicSelections { get; set; } = new List<ExamTopicSelection>();
}
```

- [ ] **Step 3: Write `ExamTopicSelection`**

One row = "this exam needs `Count` active questions of `Type`/`Difficulty` from `Topic`." `DisplayOrder` orders the topic within the exam and must be the same value for every row belonging to the same topic (enforced by the CQRS layer, not the DB).

```csharp
namespace ExamSystem.Domain.Exams;

public class ExamTopicSelection : BaseEntity
{
    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public Guid TopicId { get; set; }
    public Topic? Topic { get; set; }

    public int DisplayOrder { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public QuestionType Type { get; set; }
    public int Count { get; set; }
}
```

- [ ] **Step 4: Register the new namespace in `GlobalUsings.cs`**

Modify `src/ExamSystem.Domain/GlobalUsings.cs`:
```csharp
global using ExamSystem.Domain.Common;
global using ExamSystem.Domain.Topics;
global using ExamSystem.Domain.Questions;
global using ExamSystem.Domain.Exams;
```

- [ ] **Step 5: Build the Domain project**

Run: `dotnet build src/ExamSystem.Domain/ExamSystem.Domain.csproj`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Domain
git commit -m "feat(domain): add Exam, ExamStatus, and ExamTopicSelection entities"
```

---

### Task 2: Infrastructure — `IApplicationDbContext`, EF Configurations, Migration

**Files:**
- Modify: `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/ExamConfiguration.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/ExamTopicSelectionConfiguration.cs`

- [ ] **Step 1: Add `Exams` and `ExamTopicSelections` to `IApplicationDbContext`**

Modify `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs`:
```csharp
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Topic> Topics { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<Exam> Exams { get; }
    DbSet<ExamTopicSelection> ExamTopicSelections { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Implement the new DbSets on `ApplicationDbContext`**

Modify `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs`:
```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
}
```

- [ ] **Step 3: Write `ExamConfiguration`**

```csharp
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
```

- [ ] **Step 4: Write `ExamTopicSelectionConfiguration`**

```csharp
using ExamSystem.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class ExamTopicSelectionConfiguration : IEntityTypeConfiguration<ExamTopicSelection>
{
    public void Configure(EntityTypeBuilder<ExamTopicSelection> builder)
    {
        builder.HasOne(s => s.Exam)
            .WithMany(e => e.TopicSelections)
            .HasForeignKey(s => s.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Topic)
            .WithMany()
            .HasForeignKey(s => s.TopicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.ExamId, s.TopicId, s.Difficulty, s.Type }).IsUnique();
    }
}
```

- [ ] **Step 5: Build the solution**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 6: Generate the migration**

Run:
```bash
dotnet ef migrations add AddExamsAndExamTopicSelections --project src/ExamSystem.Infrastructure --startup-project src/ExamSystem.Api
```
Expected: "Done." and new files under `src/ExamSystem.Infrastructure/Migrations/`.

- [ ] **Step 7: Apply the migration to LocalDB**

Run:
```bash
dotnet ef database update --project src/ExamSystem.Infrastructure --startup-project src/ExamSystem.Api
```
Expected: "Done." — `Exams` and `ExamTopicSelections` tables created.

- [ ] **Step 8: Commit**

```bash
git add src/ExamSystem.Application src/ExamSystem.Infrastructure
git commit -m "feat(infrastructure): add Exams/ExamTopicSelections EF configurations and migration"
```

---

### Task 3: Application — CreateExam CQRS (TDD)

**Files:**
- Create: `src/ExamSystem.Application/Features/Exams/ExamTopicSelectionInput.cs`
- Create: `src/ExamSystem.Application/Features/Exams/CreateExam/CreateExamCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/CreateExam/CreateExamCommandValidator.cs`
- Create: `src/ExamSystem.Application/Features/Exams/CreateExam/CreateExamCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/CreateExamCommandValidatorTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/CreateExamCommandHandlerTests.cs`

- [ ] **Step 1: Write the shared `ExamTopicSelectionInput` record**

`src/ExamSystem.Application/Features/Exams/ExamTopicSelectionInput.cs`:
```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Exams;

public record ExamTopicSelectionInput(Guid TopicId, int DisplayOrder, DifficultyLevel Difficulty, QuestionType Type, int Count);
```

- [ ] **Step 2: Write `CreateExamCommand`**

```csharp
namespace ExamSystem.Application.Features.Exams.CreateExam;

public record CreateExamCommand(
    string Name,
    string? Description,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    int DurationMinutes,
    decimal McqPoints,
    decimal TrueFalsePoints,
    decimal FillBlankPoints,
    decimal PassMarkPercentage,
    int MaxAttempts,
    bool ShuffleAnswers,
    bool ShowResultImmediately,
    bool AllowBackNavigation,
    List<ExamTopicSelectionInput> TopicSelections) : IRequest<Result<Guid>>;
```

- [ ] **Step 3: Write the failing validator tests**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/CreateExamCommandValidatorTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.CreateExam;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class CreateExamCommandValidatorTests
{
    private static (CreateExamCommandValidator Validator, Guid TopicId) CreateValidatorWithActiveTopic()
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1, IsActive = true };
        db.Topics.Add(topic);
        db.SaveChanges();
        return (new CreateExamCommandValidator(db), topic.Id);
    }

    private static CreateExamCommand ValidCommand(Guid topicId, List<ExamTopicSelectionInput>? selections = null) =>
        new(
            "Excel Basics", null, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), 60,
            2m, 1m, 5m, 60m, 1, true, true, true,
            selections ?? new List<ExamTopicSelectionInput> { new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 25) });

    [Fact]
    public async Task ValidCommand_IsValid()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();

        var result = await validator.ValidateAsync(ValidCommand(topicId));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task EndBeforeStart_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId) with { StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(-1) };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task NoTopicSelections_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId, new List<ExamTopicSelectionInput>());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ZeroCountSelection_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId, new List<ExamTopicSelectionInput> { new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 0) });

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task DuplicateTopicDifficultyType_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId, new List<ExamTopicSelectionInput>
        {
            new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 10),
            new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 5)
        });

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task UnknownTopic_IsRejected()
    {
        var (validator, _) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(Guid.NewGuid());

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task PassMarkOutOfRange_IsRejected()
    {
        var (validator, topicId) = CreateValidatorWithActiveTopic();
        var command = ValidCommand(topicId) with { PassMarkPercentage = 150m };

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateExamCommandValidatorTests`
Expected: FAIL to compile — `CreateExamCommandValidator` does not exist yet.

- [ ] **Step 5: Implement `CreateExamCommandValidator`**

```csharp
namespace ExamSystem.Application.Features.Exams.CreateExam;

public class CreateExamCommandValidator : AbstractValidator<CreateExamCommand>
{
    public CreateExamCommandValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Exam name is required.");
        RuleFor(x => x.EndAtUtc).GreaterThan(x => x.StartAtUtc).WithMessage("End date must be after the start date.");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).WithMessage("Duration must be greater than zero.");
        RuleFor(x => x.PassMarkPercentage).InclusiveBetween(0, 100).WithMessage("Pass mark must be between 0 and 100.");
        RuleFor(x => x.MaxAttempts).GreaterThanOrEqualTo(1).WithMessage("Max attempts must be at least 1.");

        RuleFor(x => x.TopicSelections)
            .Must(selections => selections is { Count: > 0 })
            .WithMessage("At least one topic must be configured.");

        When(x => x.TopicSelections is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.TopicSelections)
                .ChildRules(selection =>
                {
                    selection.RuleFor(s => s.Count).GreaterThan(0).WithMessage("Question count must be greater than zero.");
                });

            RuleFor(x => x.TopicSelections)
                .Must(selections => selections
                    .GroupBy(s => (s.TopicId, s.Difficulty, s.Type))
                    .All(g => g.Count() == 1))
                .WithMessage("Each Topic/Difficulty/Type combination can only appear once.");

            RuleForEach(x => x.TopicSelections)
                .MustAsync(async (selection, ct) => await db.Topics.AnyAsync(t => t.Id == selection.TopicId && t.IsActive, ct))
                .WithMessage("Topic not found or inactive.");
        });
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateExamCommandValidatorTests`
Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 7: Write the failing test for `CreateExamCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/CreateExamCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.CreateExam;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class CreateExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidExam_PersistsExamAndTopicSelectionsAsDraft()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateExamCommandHandler(db);
        var selections = new List<ExamTopicSelectionInput>
        {
            new(topic.Id, 1, DifficultyLevel.Medium, QuestionType.Mcq, 25),
            new(topic.Id, 1, DifficultyLevel.Hard, QuestionType.FillBlank, 5)
        };
        var command = new CreateExamCommand(
            "Excel Basics", null, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), 60,
            2m, 1m, 5m, 60m, 1, true, true, true, selections);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.Exams.Include(e => e.TopicSelections).Single();
        Assert.Equal(2, saved.TopicSelections.Count);
        Assert.Equal(ExamStatus.Draft, saved.Status);
        Assert.Equal(result.Value, saved.Id);
    }
}
```

- [ ] **Step 8: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateExamCommandHandlerTests`
Expected: FAIL to compile — `CreateExamCommandHandler` does not exist yet.

- [ ] **Step 9: Implement `CreateExamCommandHandler`**

```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.CreateExam;

public class CreateExamCommandHandler : IRequestHandler<CreateExamCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateExamCommand request, CancellationToken cancellationToken)
    {
        var exam = new Exam
        {
            Name = request.Name,
            Description = request.Description,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            DurationMinutes = request.DurationMinutes,
            McqPoints = request.McqPoints,
            TrueFalsePoints = request.TrueFalsePoints,
            FillBlankPoints = request.FillBlankPoints,
            PassMarkPercentage = request.PassMarkPercentage,
            MaxAttempts = request.MaxAttempts,
            ShuffleAnswers = request.ShuffleAnswers,
            ShowResultImmediately = request.ShowResultImmediately,
            AllowBackNavigation = request.AllowBackNavigation,
            Status = ExamStatus.Draft
        };

        foreach (var selection in request.TopicSelections)
        {
            exam.TopicSelections.Add(new ExamTopicSelection
            {
                TopicId = selection.TopicId,
                DisplayOrder = selection.DisplayOrder,
                Difficulty = selection.Difficulty,
                Type = selection.Type,
                Count = selection.Count
            });
        }

        _db.Exams.Add(exam);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(exam.Id);
    }
}
```

- [ ] **Step 10: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CreateExamCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 11: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add CreateExam CQRS with validator and handler tests"
```

---

### Task 4: Application — UpdateExam & DeleteExam CQRS (TDD)

**Files:**
- Create: `src/ExamSystem.Application/Features/Exams/UpdateExam/UpdateExamCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/UpdateExam/UpdateExamCommandValidator.cs`
- Create: `src/ExamSystem.Application/Features/Exams/UpdateExam/UpdateExamCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Exams/DeleteExam/DeleteExamCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/DeleteExam/DeleteExamCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/UpdateExamCommandHandlerTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/DeleteExamCommandHandlerTests.cs`

Per FR-4.8, an Exam's configuration is only mutable while it is `Draft` — once `Published` its Topic×Difficulty×Type matrix must stay stable (a later Attempt/Snapshot will be built against it). Delete follows the same rule.

- [ ] **Step 1: Write `UpdateExamCommand`**

```csharp
namespace ExamSystem.Application.Features.Exams.UpdateExam;

public record UpdateExamCommand(
    Guid Id,
    string Name,
    string? Description,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    int DurationMinutes,
    decimal McqPoints,
    decimal TrueFalsePoints,
    decimal FillBlankPoints,
    decimal PassMarkPercentage,
    int MaxAttempts,
    bool ShuffleAnswers,
    bool ShowResultImmediately,
    bool AllowBackNavigation,
    List<ExamTopicSelectionInput> TopicSelections) : IRequest<Result<Unit>>;
```

- [ ] **Step 2: Write `UpdateExamCommandValidator`**

Same field rules as `CreateExamCommandValidator` (Task 3, Step 5) — duplicated per this codebase's established convention (see `CreateTopicCommandValidator` vs `UpdateTopicCommandValidator`, which are separate classes too).

```csharp
namespace ExamSystem.Application.Features.Exams.UpdateExam;

public class UpdateExamCommandValidator : AbstractValidator<UpdateExamCommand>
{
    public UpdateExamCommandValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Exam name is required.");
        RuleFor(x => x.EndAtUtc).GreaterThan(x => x.StartAtUtc).WithMessage("End date must be after the start date.");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).WithMessage("Duration must be greater than zero.");
        RuleFor(x => x.PassMarkPercentage).InclusiveBetween(0, 100).WithMessage("Pass mark must be between 0 and 100.");
        RuleFor(x => x.MaxAttempts).GreaterThanOrEqualTo(1).WithMessage("Max attempts must be at least 1.");

        RuleFor(x => x.TopicSelections)
            .Must(selections => selections is { Count: > 0 })
            .WithMessage("At least one topic must be configured.");

        When(x => x.TopicSelections is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.TopicSelections)
                .ChildRules(selection =>
                {
                    selection.RuleFor(s => s.Count).GreaterThan(0).WithMessage("Question count must be greater than zero.");
                });

            RuleFor(x => x.TopicSelections)
                .Must(selections => selections
                    .GroupBy(s => (s.TopicId, s.Difficulty, s.Type))
                    .All(g => g.Count() == 1))
                .WithMessage("Each Topic/Difficulty/Type combination can only appear once.");

            RuleForEach(x => x.TopicSelections)
                .MustAsync(async (selection, ct) => await db.Topics.AnyAsync(t => t.Id == selection.TopicId && t.IsActive, ct))
                .WithMessage("Topic not found or inactive.");
        });
    }
}
```

- [ ] **Step 3: Write the failing tests for `UpdateExamCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/UpdateExamCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.UpdateExam;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class UpdateExamCommandHandlerTests
{
    private static async Task<(ApplicationDbContext Db, Exam Exam, Guid TopicId)> SeedDraftExamAsync()
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var exam = new Exam { Name = "Original", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 30 };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 5 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, exam, topic.Id);
    }

    [Fact]
    public async Task Handle_DraftExam_UpdatesFieldsAndReplacesTopicSelections()
    {
        var (db, exam, topicId) = await SeedDraftExamAsync();
        var handler = new UpdateExamCommandHandler(db);
        var newSelections = new List<ExamTopicSelectionInput> { new(topicId, 1, DifficultyLevel.Hard, QuestionType.FillBlank, 3) };
        var command = new UpdateExamCommand(
            exam.Id, "Renamed", "desc", DateTime.UtcNow, DateTime.UtcNow.AddDays(14), 45,
            3m, 1m, 6m, 70m, 2, false, false, false, newSelections);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updated = db.Exams.Include(e => e.TopicSelections).Single();
        Assert.Equal("Renamed", updated.Name);
        Assert.Single(updated.TopicSelections);
        Assert.Equal(DifficultyLevel.Hard, updated.TopicSelections.Single().Difficulty);
    }

    [Fact]
    public async Task Handle_PublishedExam_ReturnsFailureAndLeavesItUnchanged()
    {
        var (db, exam, topicId) = await SeedDraftExamAsync();
        exam.Status = ExamStatus.Published;
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateExamCommandHandler(db);
        var command = new UpdateExamCommand(
            exam.Id, "Renamed", null, DateTime.UtcNow, DateTime.UtcNow.AddDays(14), 45,
            2m, 1m, 5m, 60m, 1, true, true, true,
            new List<ExamTopicSelectionInput> { new(topicId, 1, DifficultyLevel.Medium, QuestionType.Mcq, 5) });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Draft exams can be edited.", result.Errors);
        Assert.Equal("Original", db.Exams.Single().Name);
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter UpdateExamCommandHandlerTests`
Expected: FAIL to compile — `UpdateExamCommandHandler` does not exist yet.

- [ ] **Step 5: Implement `UpdateExamCommandHandler`**

```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.UpdateExam;

public class UpdateExamCommandHandler : IRequestHandler<UpdateExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public UpdateExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(UpdateExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Draft)
        {
            return Result<Unit>.Failure("Only Draft exams can be edited.");
        }

        exam.Name = request.Name;
        exam.Description = request.Description;
        exam.StartAtUtc = request.StartAtUtc;
        exam.EndAtUtc = request.EndAtUtc;
        exam.DurationMinutes = request.DurationMinutes;
        exam.McqPoints = request.McqPoints;
        exam.TrueFalsePoints = request.TrueFalsePoints;
        exam.FillBlankPoints = request.FillBlankPoints;
        exam.PassMarkPercentage = request.PassMarkPercentage;
        exam.MaxAttempts = request.MaxAttempts;
        exam.ShuffleAnswers = request.ShuffleAnswers;
        exam.ShowResultImmediately = request.ShowResultImmediately;
        exam.AllowBackNavigation = request.AllowBackNavigation;
        exam.ModifiedAtUtc = DateTime.UtcNow;

        exam.TopicSelections.Clear();
        foreach (var selection in request.TopicSelections)
        {
            exam.TopicSelections.Add(new ExamTopicSelection
            {
                TopicId = selection.TopicId,
                DisplayOrder = selection.DisplayOrder,
                Difficulty = selection.Difficulty,
                Type = selection.Type,
                Count = selection.Count
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter UpdateExamCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 7: Write the failing tests for `DeleteExamCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/DeleteExamCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.DeleteExam;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class DeleteExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_DraftExam_DeletesIt()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Draft Exam", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 30 };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteExamCommandHandler(db);
        var result = await handler.Handle(new DeleteExamCommand(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(db.Exams);
    }

    [Fact]
    public async Task Handle_PublishedExam_ReturnsFailureAndKeepsIt()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Published Exam", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 30, Status = ExamStatus.Published };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new DeleteExamCommandHandler(db);
        var result = await handler.Handle(new DeleteExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Draft exams can be deleted -- archive it instead.", result.Errors);
        Assert.Single(db.Exams);
    }
}
```

- [ ] **Step 8: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter DeleteExamCommandHandlerTests`
Expected: FAIL to compile — `DeleteExamCommand`/`DeleteExamCommandHandler` do not exist yet.

- [ ] **Step 9: Implement `DeleteExamCommand` and handler**

`src/ExamSystem.Application/Features/Exams/DeleteExam/DeleteExamCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.DeleteExam;

public record DeleteExamCommand(Guid Id) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Exams/DeleteExam/DeleteExamCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.DeleteExam;

public class DeleteExamCommandHandler : IRequestHandler<DeleteExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public DeleteExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(DeleteExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Draft)
        {
            return Result<Unit>.Failure("Only Draft exams can be deleted -- archive it instead.");
        }

        _db.Exams.Remove(exam);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 10: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter DeleteExamCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 11: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add UpdateExam and DeleteExam CQRS with tests"
```

---

### Task 5: Application — GetExams & GetExamById Queries (TDD)

**Files:**
- Create: `src/ExamSystem.Application/Features/Exams/GetExams/ExamSummaryDto.cs`
- Create: `src/ExamSystem.Application/Features/Exams/GetExams/GetExamsQuery.cs`
- Create: `src/ExamSystem.Application/Features/Exams/GetExams/GetExamsQueryHandler.cs`
- Create: `src/ExamSystem.Application/Features/Exams/GetExamById/ExamTopicSelectionDto.cs`
- Create: `src/ExamSystem.Application/Features/Exams/GetExamById/ExamDetailDto.cs`
- Create: `src/ExamSystem.Application/Features/Exams/GetExamById/GetExamByIdQuery.cs`
- Create: `src/ExamSystem.Application/Features/Exams/GetExamById/GetExamByIdQueryHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/GetExamsQueryHandlerTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/GetExamByIdQueryHandlerTests.cs`

- [ ] **Step 1: Write the failing test for `GetExamsQueryHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/GetExamsQueryHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.GetExams;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class GetExamsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTotalQuestionCountAndTotalPointsPerExam()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 60, McqPoints = 2m, FillBlankPoints = 5m };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Hard, Type = QuestionType.FillBlank, Count = 5 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamsQueryHandler(db);
        var result = await handler.Handle(new GetExamsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value!);
        Assert.Equal(30, dto.TotalQuestionCount);
        Assert.Equal(75m, dto.TotalPoints);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter GetExamsQueryHandlerTests`
Expected: FAIL to compile — none of the `GetExams` types exist yet.

- [ ] **Step 3: Implement `ExamSummaryDto`, `GetExamsQuery`, and handler**

`src/ExamSystem.Application/Features/Exams/GetExams/ExamSummaryDto.cs`:
```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.GetExams;

public record ExamSummaryDto(
    Guid Id, string Name, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
    ExamStatus Status, int TotalQuestionCount, decimal TotalPoints);
```

`src/ExamSystem.Application/Features/Exams/GetExams/GetExamsQuery.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.GetExams;

public record GetExamsQuery : IRequest<Result<List<ExamSummaryDto>>>;
```

`src/ExamSystem.Application/Features/Exams/GetExams/GetExamsQueryHandler.cs`:
```csharp
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Exams.GetExams;

public class GetExamsQueryHandler : IRequestHandler<GetExamsQuery, Result<List<ExamSummaryDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetExamsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<ExamSummaryDto>>> Handle(GetExamsQuery request, CancellationToken cancellationToken)
    {
        var exams = await _db.Exams.Include(e => e.TopicSelections).ToListAsync(cancellationToken);

        var dtos = exams
            .Select(e => new ExamSummaryDto(
                e.Id, e.Name, e.StartAtUtc, e.EndAtUtc, e.DurationMinutes, e.Status,
                e.TopicSelections.Sum(s => s.Count),
                e.TopicSelections.Sum(s => s.Count * PointsFor(e, s.Type))))
            .OrderByDescending(d => d.StartAtUtc)
            .ToList();

        return Result<List<ExamSummaryDto>>.Success(dtos);
    }

    private static decimal PointsFor(Exam exam, QuestionType type) => type switch
    {
        QuestionType.Mcq => exam.McqPoints,
        QuestionType.TrueFalse => exam.TrueFalsePoints,
        QuestionType.FillBlank => exam.FillBlankPoints,
        _ => 0m
    };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter GetExamsQueryHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 5: Write the failing test for `GetExamByIdQueryHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/GetExamByIdQueryHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.GetExamById;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class GetExamByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingExam_ReturnsDetailWithTopicNames()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 60 };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamByIdQueryHandler(db);
        var result = await handler.Handle(new GetExamByIdQuery(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Excel", result.Value!.TopicSelections.Single().TopicName);
    }

    [Fact]
    public async Task Handle_UnknownExam_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new GetExamByIdQueryHandler(db);

        var result = await handler.Handle(new GetExamByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter GetExamByIdQueryHandlerTests`
Expected: FAIL to compile — none of the `GetExamById` types exist yet.

- [ ] **Step 7: Implement `ExamTopicSelectionDto`, `ExamDetailDto`, `GetExamByIdQuery`, and handler**

`src/ExamSystem.Application/Features/Exams/GetExamById/ExamTopicSelectionDto.cs`:
```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Application.Features.Exams.GetExamById;

public record ExamTopicSelectionDto(Guid TopicId, string TopicName, int DisplayOrder, DifficultyLevel Difficulty, QuestionType Type, int Count);
```

`src/ExamSystem.Application/Features/Exams/GetExamById/ExamDetailDto.cs`:
```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.GetExamById;

public record ExamDetailDto(
    Guid Id, string Name, string? Description, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
    decimal McqPoints, decimal TrueFalsePoints, decimal FillBlankPoints, decimal PassMarkPercentage, int MaxAttempts,
    bool ShuffleAnswers, bool ShowResultImmediately, bool AllowBackNavigation, ExamStatus Status,
    List<ExamTopicSelectionDto> TopicSelections);
```

`src/ExamSystem.Application/Features/Exams/GetExamById/GetExamByIdQuery.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.GetExamById;

public record GetExamByIdQuery(Guid Id) : IRequest<Result<ExamDetailDto>>;
```

`src/ExamSystem.Application/Features/Exams/GetExamById/GetExamByIdQueryHandler.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.GetExamById;

public class GetExamByIdQueryHandler : IRequestHandler<GetExamByIdQuery, Result<ExamDetailDto>>
{
    private readonly IApplicationDbContext _db;

    public GetExamByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ExamDetailDto>> Handle(GetExamByIdQuery request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .ThenInclude(s => s.Topic)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (exam is null)
        {
            return Result<ExamDetailDto>.Failure("Exam not found.");
        }

        var dto = new ExamDetailDto(
            exam.Id, exam.Name, exam.Description, exam.StartAtUtc, exam.EndAtUtc, exam.DurationMinutes,
            exam.McqPoints, exam.TrueFalsePoints, exam.FillBlankPoints, exam.PassMarkPercentage, exam.MaxAttempts,
            exam.ShuffleAnswers, exam.ShowResultImmediately, exam.AllowBackNavigation, exam.Status,
            exam.TopicSelections
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new ExamTopicSelectionDto(s.TopicId, s.Topic!.Name, s.DisplayOrder, s.Difficulty, s.Type, s.Count))
                .ToList());

        return Result<ExamDetailDto>.Success(dto);
    }
}
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter GetExamByIdQueryHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 9: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add GetExams and GetExamById queries with tests"
```

---

### Task 6: Application — Publish/Close/Archive Lifecycle Commands (TDD)

**Files:**
- Create: `src/ExamSystem.Application/Features/Exams/PublishExam/PublishExamCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/PublishExam/PublishExamCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Exams/CloseExam/CloseExamCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/CloseExam/CloseExamCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Exams/ArchiveExam/ArchiveExamCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/ArchiveExam/ArchiveExamCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/PublishExamCommandHandlerTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/CloseExamCommandHandlerTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/ArchiveExamCommandHandlerTests.cs`

This is the FR-4.9 publish-validation task — the most important one in this plan.

- [ ] **Step 1: Write the failing tests for `PublishExamCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/PublishExamCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.PublishExam;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class PublishExamCommandHandlerTests
{
    private static async Task<(ApplicationDbContext Db, Exam Exam)> SeedDraftExamAsync(int mcqNeeded, int mcqAvailable)
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);

        for (var i = 0; i < mcqAvailable; i++)
        {
            var question = new Question { TopicId = topic.Id, Text = $"Q{i}", Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium, IsActive = true };
            question.Options.Add(new QuestionOption { Text = "A", IsCorrect = false });
            question.Options.Add(new QuestionOption { Text = "B", IsCorrect = true });
            db.Questions.Add(question);
        }

        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow.AddHours(1), EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 60 };
        exam.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = mcqNeeded });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        return (db, exam);
    }

    [Fact]
    public async Task Handle_SufficientQuestionBank_PublishesExam()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 5, mcqAvailable: 5);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExamStatus.Published, db.Exams.Single().Status);
    }

    [Fact]
    public async Task Handle_InsufficientQuestionBank_ReturnsFailureAndStaysDraft()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 10, mcqAvailable: 3);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("only 3 are available"));
        Assert.Equal(ExamStatus.Draft, db.Exams.Single().Status);
    }

    [Fact]
    public async Task Handle_AlreadyPublishedExam_ReturnsFailure()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 1, mcqAvailable: 1);
        exam.Status = ExamStatus.Published;
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Draft exams can be published.", result.Errors);
    }

    [Fact]
    public async Task Handle_EndDateInThePast_ReturnsFailure()
    {
        var (db, exam) = await SeedDraftExamAsync(mcqNeeded: 1, mcqAvailable: 1);
        exam.EndAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new PublishExamCommandHandler(db);

        var result = await handler.Handle(new PublishExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("End date must be in the future.", result.Errors);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter PublishExamCommandHandlerTests`
Expected: FAIL to compile — `PublishExamCommand`/`PublishExamCommandHandler` do not exist yet.

- [ ] **Step 3: Implement `PublishExamCommand` and handler**

`src/ExamSystem.Application/Features/Exams/PublishExam/PublishExamCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.PublishExam;

public record PublishExamCommand(Guid Id) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Exams/PublishExam/PublishExamCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.PublishExam;

public class PublishExamCommandHandler : IRequestHandler<PublishExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public PublishExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(PublishExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .ThenInclude(s => s.Topic)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Draft)
        {
            return Result<Unit>.Failure("Only Draft exams can be published.");
        }

        var errors = new List<string>();

        if (exam.StartAtUtc >= exam.EndAtUtc)
        {
            errors.Add("Start date must be before the end date.");
        }

        if (exam.EndAtUtc <= DateTime.UtcNow)
        {
            errors.Add("End date must be in the future.");
        }

        if (exam.TopicSelections.Count == 0)
        {
            errors.Add("Exam has no topics configured.");
        }

        foreach (var selection in exam.TopicSelections)
        {
            var available = await _db.Questions.CountAsync(
                q => q.TopicId == selection.TopicId && q.Difficulty == selection.Difficulty
                     && q.Type == selection.Type && q.IsActive,
                cancellationToken);

            if (available < selection.Count)
            {
                errors.Add(
                    $"Topic '{selection.Topic!.Name}' needs {selection.Count} {selection.Type}/{selection.Difficulty} " +
                    $"question(s) but only {available} are available.");
            }
        }

        if (errors.Count > 0)
        {
            return Result<Unit>.Failure(errors);
        }

        exam.Status = ExamStatus.Published;
        exam.ModifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter PublishExamCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: Write the failing tests for `CloseExamCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/CloseExamCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.CloseExam;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class CloseExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_PublishedExam_ClosesIt()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60, Status = ExamStatus.Published };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new CloseExamCommandHandler(db);

        var result = await handler.Handle(new CloseExamCommand(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExamStatus.Closed, db.Exams.Single().Status);
    }

    [Fact]
    public async Task Handle_DraftExam_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60 };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new CloseExamCommandHandler(db);

        var result = await handler.Handle(new CloseExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Published exams can be closed.", result.Errors);
    }
}
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CloseExamCommandHandlerTests`
Expected: FAIL to compile — `CloseExamCommand`/`CloseExamCommandHandler` do not exist yet.

- [ ] **Step 7: Implement `CloseExamCommand` and handler**

`src/ExamSystem.Application/Features/Exams/CloseExam/CloseExamCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.CloseExam;

public record CloseExamCommand(Guid Id) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Exams/CloseExam/CloseExamCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.CloseExam;

public class CloseExamCommandHandler : IRequestHandler<CloseExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public CloseExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(CloseExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Published)
        {
            return Result<Unit>.Failure("Only Published exams can be closed.");
        }

        exam.Status = ExamStatus.Closed;
        exam.ModifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CloseExamCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 9: Write the failing tests for `ArchiveExamCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/ArchiveExamCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.ArchiveExam;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class ArchiveExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_ClosedExam_ArchivesIt()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60, Status = ExamStatus.Closed };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new ArchiveExamCommandHandler(db);

        var result = await handler.Handle(new ArchiveExamCommand(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExamStatus.Archived, db.Exams.Single().Status);
    }

    [Fact]
    public async Task Handle_DraftExam_ReturnsFailure()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "Excel Basics", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60 };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        var handler = new ArchiveExamCommandHandler(db);

        var result = await handler.Handle(new ArchiveExamCommand(exam.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only Closed exams can be archived.", result.Errors);
    }
}
```

- [ ] **Step 10: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter ArchiveExamCommandHandlerTests`
Expected: FAIL to compile — `ArchiveExamCommand`/`ArchiveExamCommandHandler` do not exist yet.

- [ ] **Step 11: Implement `ArchiveExamCommand` and handler**

`src/ExamSystem.Application/Features/Exams/ArchiveExam/ArchiveExamCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.ArchiveExam;

public record ArchiveExamCommand(Guid Id) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Exams/ArchiveExam/ArchiveExamCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.ArchiveExam;

public class ArchiveExamCommandHandler : IRequestHandler<ArchiveExamCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public ArchiveExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(ArchiveExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Closed)
        {
            return Result<Unit>.Failure("Only Closed exams can be archived.");
        }

        exam.Status = ExamStatus.Archived;
        exam.ModifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

- [ ] **Step 12: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter ArchiveExamCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 13: Run the full Application test suite**

Run: `dotnet test tests/ExamSystem.Application.UnitTests`
Expected: all existing + new tests pass, 0 failures.

- [ ] **Step 14: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add Publish/Close/Archive exam lifecycle commands with tests"
```

---

### Task 7: API — `ExamsController` + Integration Tests

**Files:**
- Create: `src/ExamSystem.Api/Controllers/ExamsController.cs`
- Create: `tests/ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs`

- [ ] **Step 1: Write the failing integration tests**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs`:
```csharp
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class ExamsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ExamsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<Guid> CreateTopicAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/admin/topics", new { name, displayOrder = 1 });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Id;
    }

    private static async Task CreateMcqQuestionAsync(HttpClient client, Guid topicId, string difficulty)
    {
        var response = await client.PostAsJsonAsync("/api/admin/questions", new
        {
            topicId,
            type = "Mcq",
            difficulty,
            text = "Pick one",
            options = new[] { new { text = "A", isCorrect = false }, new { text = "B", isCorrect = true } }
        });
        response.EnsureSuccessStatusCode();
    }

    private static object BuildExamPayload(Guid topicId, int count) => new
    {
        name = $"Exam {Guid.NewGuid():N}",
        description = (string?)null,
        startAtUtc = DateTime.UtcNow.AddDays(1),
        endAtUtc = DateTime.UtcNow.AddDays(8),
        durationMinutes = 60,
        mcqPoints = 2m,
        trueFalsePoints = 1m,
        fillBlankPoints = 5m,
        passMarkPercentage = 60m,
        maxAttempts = 1,
        shuffleAnswers = true,
        showResultImmediately = true,
        allowBackNavigation = true,
        topicSelections = new[] { new { topicId, displayOrder = 1, difficulty = "Medium", type = "Mcq", count } }
    };

    [Fact]
    public async Task CreateThenGet_ReturnsTheCreatedExamAsDraft()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - Exams Create");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, 1));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var getResponse = await client.GetAsync($"/api/admin/exams/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var detail = await getResponse.Content.ReadFromJsonAsync<ExamDetailResponse>();
        Assert.Equal("Draft", detail!.Status);
        Assert.Single(detail.TopicSelections);
    }

    [Fact]
    public async Task Publish_WithInsufficientQuestionBank_ReturnsBadRequest()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - Publish Fail");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, count: 5));
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var publishResponse = await client.PostAsync($"/api/admin/exams/{created!.Id}/publish", null);

        Assert.Equal(HttpStatusCode.BadRequest, publishResponse.StatusCode);
    }

    [Fact]
    public async Task Publish_WithSufficientQuestionBank_SucceedsThenCloseThenArchive()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - Full Lifecycle");
        await CreateMcqQuestionAsync(client, topicId, "Medium");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, count: 1));
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var publishResponse = await client.PostAsync($"/api/admin/exams/{created!.Id}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publishResponse.StatusCode);

        var closeResponse = await client.PostAsync($"/api/admin/exams/{created.Id}/close", null);
        Assert.Equal(HttpStatusCode.NoContent, closeResponse.StatusCode);

        var archiveResponse = await client.PostAsync($"/api/admin/exams/{created.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/admin/exams/{created.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<ExamDetailResponse>();
        Assert.Equal("Archived", detail!.Status);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(Guid.NewGuid(), 1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record IdResponse(Guid Id);
    private record ExamDetailResponse(Guid Id, string Status, List<object> TopicSelections);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter ExamsControllerTests`
Expected: FAIL — 404 Not Found (no `ExamsController` yet).

- [ ] **Step 3: Implement `ExamsController`**

```csharp
using ExamSystem.Application.Features.Exams;
using ExamSystem.Application.Features.Exams.ArchiveExam;
using ExamSystem.Application.Features.Exams.CloseExam;
using ExamSystem.Application.Features.Exams.CreateExam;
using ExamSystem.Application.Features.Exams.DeleteExam;
using ExamSystem.Application.Features.Exams.GetExamById;
using ExamSystem.Application.Features.Exams.GetExams;
using ExamSystem.Application.Features.Exams.PublishExam;
using ExamSystem.Application.Features.Exams.UpdateExam;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Admin CRUD and lifecycle management for Exams.</summary>
[ApiController]
[Route("api/admin/exams")]
[Authorize(Roles = "Admin")]
public class ExamsController : ControllerBase
{
    private readonly ISender _sender;

    public ExamsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamByIdQuery(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateExamCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExamRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateExamCommand(
            id, request.Name, request.Description, request.StartAtUtc, request.EndAtUtc, request.DurationMinutes,
            request.McqPoints, request.TrueFalsePoints, request.FillBlankPoints, request.PassMarkPercentage,
            request.MaxAttempts, request.ShuffleAnswers, request.ShowResultImmediately, request.AllowBackNavigation,
            request.TopicSelections);

        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PublishExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CloseExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    public record UpdateExamRequest(
        string Name, string? Description, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
        decimal McqPoints, decimal TrueFalsePoints, decimal FillBlankPoints, decimal PassMarkPercentage, int MaxAttempts,
        bool ShuffleAnswers, bool ShowResultImmediately, bool AllowBackNavigation,
        List<ExamTopicSelectionInput> TopicSelections);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter ExamsControllerTests`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: Run the entire backend test suite**

Run: `dotnet test`
Expected: all projects `Passed!`, 0 failures.

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Api tests/ExamSystem.Api.IntegrationTests
git commit -m "feat(api): add ExamsController with CRUD and lifecycle endpoints, integration tests"
```

---

### Task 8: Frontend — `exam.service.ts`

**Files:**
- Create: `frontend/src/app/core/services/exam.service.ts`

- [ ] **Step 1: Write `exam.service.ts`**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type ExamStatus = 'Draft' | 'Published' | 'Closed' | 'Archived';
export type Difficulty = 'Easy' | 'Medium' | 'Hard';
export type QuestionType = 'Mcq' | 'TrueFalse' | 'FillBlank';

export interface ExamTopicSelectionInput {
  topicId: string;
  displayOrder: number;
  difficulty: Difficulty;
  type: QuestionType;
  count: number;
}

export interface ExamTopicSelectionDto extends ExamTopicSelectionInput {
  topicName: string;
}

export interface ExamInput {
  name: string;
  description: string | null;
  startAtUtc: string;
  endAtUtc: string;
  durationMinutes: number;
  mcqPoints: number;
  trueFalsePoints: number;
  fillBlankPoints: number;
  passMarkPercentage: number;
  maxAttempts: number;
  shuffleAnswers: boolean;
  showResultImmediately: boolean;
  allowBackNavigation: boolean;
  topicSelections: ExamTopicSelectionInput[];
}

export interface ExamSummary {
  id: string;
  name: string;
  startAtUtc: string;
  endAtUtc: string;
  durationMinutes: number;
  status: ExamStatus;
  totalQuestionCount: number;
  totalPoints: number;
}

export interface ExamDetail extends ExamInput {
  id: string;
  status: ExamStatus;
  topicSelections: ExamTopicSelectionDto[];
}

@Injectable({ providedIn: 'root' })
export class ExamService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/exams`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<ExamSummary[]> {
    return this.http.get<ExamSummary[]>(this.baseUrl);
  }

  getById(id: string): Observable<ExamDetail> {
    return this.http.get<ExamDetail>(`${this.baseUrl}/${id}`);
  }

  create(input: ExamInput): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, input);
  }

  update(id: string, input: ExamInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, input);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  publish(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/publish`, null);
  }

  close(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/close`, null);
  }

  archive(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/archive`, null);
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/app/core/services/exam.service.ts
git commit -m "feat(frontend): add ExamService for admin exam configuration API"
```

---

### Task 9: Frontend — `exam-form.component` (Topic × Difficulty × Type matrix)

**Files:**
- Create: `frontend/src/app/features/admin/exams/exam-form.component.ts`
- Create: `frontend/src/app/features/admin/exams/exam-form.component.html`

- [ ] **Step 1: Write `exam-form.component.ts`**

```typescript
import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Topic } from '../../../core/services/topic.service';
import { Difficulty, ExamDetail, ExamInput, ExamTopicSelectionInput, QuestionType } from '../../../core/services/exam.service';

interface MatrixCell {
  control: string;
  difficulty: Difficulty;
  type: QuestionType;
  label: string;
}

const MATRIX_CELLS: MatrixCell[] = [
  { control: 'easyMcq', difficulty: 'Easy', type: 'Mcq', label: 'سهل / اختيار' },
  { control: 'easyFill', difficulty: 'Easy', type: 'FillBlank', label: 'سهل / أكمل' },
  { control: 'mediumMcq', difficulty: 'Medium', type: 'Mcq', label: 'متوسط / اختيار' },
  { control: 'mediumFill', difficulty: 'Medium', type: 'FillBlank', label: 'متوسط / أكمل' },
  { control: 'hardMcq', difficulty: 'Hard', type: 'Mcq', label: 'متقدم / اختيار' },
  { control: 'hardFill', difficulty: 'Hard', type: 'FillBlank', label: 'متقدم / أكمل' }
];

@Component({
  selector: 'app-exam-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './exam-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExamFormComponent implements OnInit, OnChanges {
  @Input() topics: Topic[] = [];
  @Input() initialValue: ExamDetail | null = null;
  @Output() save = new EventEmitter<ExamInput>();

  readonly matrixCells = MATRIX_CELLS;
  validationError: string | null = null;

  readonly form: FormGroup = this.fb.group({
    name: [''],
    description: [''],
    startAtUtc: [''],
    endAtUtc: [''],
    durationMinutes: [60],
    mcqPoints: [2],
    trueFalsePoints: [1],
    fillBlankPoints: [5],
    passMarkPercentage: [60],
    maxAttempts: [1],
    shuffleAnswers: [true],
    showResultImmediately: [true],
    allowBackNavigation: [true],
    topicRows: this.fb.array([])
  });

  constructor(private readonly fb: FormBuilder) {}

  ngOnInit(): void {
    this.rebuildForm();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['topics'] || changes['initialValue']) {
      this.rebuildForm();
    }
  }

  get topicRows(): FormArray {
    return this.form.get('topicRows') as FormArray;
  }

  private rebuildForm(): void {
    const rows = this.topics.map(topic => {
      const existing = this.initialValue?.topicSelections.filter(s => s.topicId === topic.id) ?? [];
      const group: Record<string, unknown> = {};
      for (const cell of MATRIX_CELLS) {
        const match = existing.find(s => s.difficulty === cell.difficulty && s.type === cell.type);
        group[cell.control] = [match?.count ?? 0];
      }
      return this.fb.group(group);
    });
    this.form.setControl('topicRows', this.fb.array(rows));

    if (this.initialValue) {
      this.form.patchValue({
        name: this.initialValue.name,
        description: this.initialValue.description,
        startAtUtc: this.toLocalInputValue(this.initialValue.startAtUtc),
        endAtUtc: this.toLocalInputValue(this.initialValue.endAtUtc),
        durationMinutes: this.initialValue.durationMinutes,
        mcqPoints: this.initialValue.mcqPoints,
        trueFalsePoints: this.initialValue.trueFalsePoints,
        fillBlankPoints: this.initialValue.fillBlankPoints,
        passMarkPercentage: this.initialValue.passMarkPercentage,
        maxAttempts: this.initialValue.maxAttempts,
        shuffleAnswers: this.initialValue.shuffleAnswers,
        showResultImmediately: this.initialValue.showResultImmediately,
        allowBackNavigation: this.initialValue.allowBackNavigation
      });
    } else {
      this.form.patchValue({
        name: '', description: '', startAtUtc: '', endAtUtc: '', durationMinutes: 60,
        mcqPoints: 2, trueFalsePoints: 1, fillBlankPoints: 5, passMarkPercentage: 60, maxAttempts: 1,
        shuffleAnswers: true, showResultImmediately: true, allowBackNavigation: true
      });
    }
  }

  private toLocalInputValue(isoUtc: string): string {
    return new Date(isoUtc).toISOString().slice(0, 16);
  }

  submit(): void {
    const value = this.form.value;

    if (!value.name || !value.startAtUtc || !value.endAtUtc) {
      this.validationError = 'الاسم وتاريخ البداية والنهاية مطلوبة.';
      return;
    }

    if (new Date(value.endAtUtc) <= new Date(value.startAtUtc)) {
      this.validationError = 'تاريخ النهاية يجب أن يكون بعد تاريخ البداية.';
      return;
    }

    const topicSelections: ExamTopicSelectionInput[] = [];
    this.topics.forEach((topic, index) => {
      const row = value.topicRows[index];
      MATRIX_CELLS.forEach(cell => {
        const count = Number(row[cell.control]);
        if (count > 0) {
          topicSelections.push({ topicId: topic.id, displayOrder: index + 1, difficulty: cell.difficulty, type: cell.type, count });
        }
      });
    });

    if (topicSelections.length === 0) {
      this.validationError = 'حدد عدد أسئلة واحد على الأقل من موضوع ومستوى صعوبة.';
      return;
    }

    this.validationError = null;
    this.save.emit({
      name: value.name,
      description: value.description || null,
      startAtUtc: new Date(value.startAtUtc).toISOString(),
      endAtUtc: new Date(value.endAtUtc).toISOString(),
      durationMinutes: Number(value.durationMinutes),
      mcqPoints: Number(value.mcqPoints),
      trueFalsePoints: Number(value.trueFalsePoints),
      fillBlankPoints: Number(value.fillBlankPoints),
      passMarkPercentage: Number(value.passMarkPercentage),
      maxAttempts: Number(value.maxAttempts),
      shuffleAnswers: value.shuffleAnswers,
      showResultImmediately: value.showResultImmediately,
      allowBackNavigation: value.allowBackNavigation,
      topicSelections
    });
  }
}
```

- [ ] **Step 2: Write `exam-form.component.html`**

```html
<form [formGroup]="form" (ngSubmit)="submit()" class="exam-form">
  <div class="field">
    <label for="examName">اسم الامتحان</label>
    <input id="examName" type="text" formControlName="name" />
  </div>

  <div class="field">
    <label for="examDescription">الوصف</label>
    <textarea id="examDescription" formControlName="description"></textarea>
  </div>

  <div class="field-row">
    <div class="field">
      <label for="startAtUtc">بداية الإتاحة</label>
      <input id="startAtUtc" type="datetime-local" formControlName="startAtUtc" />
    </div>
    <div class="field">
      <label for="endAtUtc">نهاية الإتاحة</label>
      <input id="endAtUtc" type="datetime-local" formControlName="endAtUtc" />
    </div>
    <div class="field">
      <label for="durationMinutes">المدة (دقيقة)</label>
      <input id="durationMinutes" type="number" min="1" formControlName="durationMinutes" />
    </div>
  </div>

  <div class="field-row">
    <div class="field">
      <label for="mcqPoints">درجة سؤال الاختيار</label>
      <input id="mcqPoints" type="number" min="0" step="0.5" formControlName="mcqPoints" />
    </div>
    <div class="field">
      <label for="trueFalsePoints">درجة سؤال صح/خطأ</label>
      <input id="trueFalsePoints" type="number" min="0" step="0.5" formControlName="trueFalsePoints" />
    </div>
    <div class="field">
      <label for="fillBlankPoints">درجة سؤال أكمل</label>
      <input id="fillBlankPoints" type="number" min="0" step="0.5" formControlName="fillBlankPoints" />
    </div>
  </div>

  <div class="field-row">
    <div class="field">
      <label for="passMarkPercentage">درجة النجاح (%)</label>
      <input id="passMarkPercentage" type="number" min="0" max="100" formControlName="passMarkPercentage" />
    </div>
    <div class="field">
      <label for="maxAttempts">عدد المحاولات المسموحة</label>
      <input id="maxAttempts" type="number" min="1" formControlName="maxAttempts" />
    </div>
  </div>

  <div class="field-row toggles">
    <label><input type="checkbox" formControlName="shuffleAnswers" /> خلط الإجابات</label>
    <label><input type="checkbox" formControlName="showResultImmediately" /> عرض النتيجة فورًا</label>
    <label><input type="checkbox" formControlName="allowBackNavigation" /> السماح بالرجوع للسؤال السابق</label>
  </div>

  <h3>توزيع الأسئلة لكل موضوع</h3>
  <table class="matrix-table" formArrayName="topicRows">
    <thead>
      <tr>
        <th>الموضوع</th>
        <th *ngFor="let cell of matrixCells">{{ cell.label }}</th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let topic of topics; let i = index" [formGroupName]="i">
        <td>{{ topic.name }}</td>
        <td *ngFor="let cell of matrixCells">
          <input type="number" min="0" [formControlName]="cell.control" />
        </td>
      </tr>
    </tbody>
  </table>

  <p class="error" *ngIf="validationError">{{ validationError }}</p>

  <button type="submit">حفظ</button>
</form>
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/features/admin/exams/exam-form.component.ts frontend/src/app/features/admin/exams/exam-form.component.html
git commit -m "feat(frontend): add exam-form component with Topic x Difficulty x Type matrix"
```

---

### Task 10: Frontend — `exams-list.component` + Routing + Lifecycle Actions

**Files:**
- Create: `frontend/src/app/features/admin/exams/exams-list.component.ts`
- Create: `frontend/src/app/features/admin/exams/exams-list.component.html`
- Modify: `frontend/src/app/app.routes.ts`

- [ ] **Step 1: Write `exams-list.component.ts`**

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExamDetail, ExamService, ExamSummary } from '../../../core/services/exam.service';
import { Topic, TopicService } from '../../../core/services/topic.service';
import { ExamFormComponent } from './exam-form.component';

@Component({
  selector: 'app-exams-list',
  standalone: true,
  imports: [CommonModule, ExamFormComponent],
  templateUrl: './exams-list.component.html'
})
export class ExamsListComponent implements OnInit {
  topics = signal<Topic[]>([]);
  exams = signal<ExamSummary[]>([]);
  editingExam = signal<ExamDetail | null>(null);
  isFormOpen = signal(false);
  errorMessage: string | null = null;

  constructor(
    private readonly examService: ExamService,
    private readonly topicService: TopicService
  ) {}

  ngOnInit(): void {
    this.topicService.getAll().subscribe(topics => this.topics.set(topics));
    this.load();
  }

  load(): void {
    this.examService.getAll().subscribe(exams => this.exams.set(exams));
  }

  openCreateForm(): void {
    this.editingExam.set(null);
    this.isFormOpen.set(true);
  }

  openEditForm(id: string): void {
    this.examService.getById(id).subscribe(exam => {
      this.editingExam.set(exam);
      this.isFormOpen.set(true);
    });
  }

  closeForm(): void {
    this.isFormOpen.set(false);
    this.editingExam.set(null);
  }

  onSave(input: import('../../../core/services/exam.service').ExamInput): void {
    this.errorMessage = null;
    const editing = this.editingExam();
    const request = editing ? this.examService.update(editing.id, input) : this.examService.create(input);

    request.subscribe({
      next: () => {
        this.closeForm();
        this.load();
      },
      error: () => (this.errorMessage = 'تعذّر حفظ الامتحان — تحقق من الحقول والتوزيع.')
    });
  }

  deleteExam(id: string): void {
    this.errorMessage = null;
    this.examService.delete(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'لا يمكن حذف امتحان تم نشره — قم بأرشفته بدلاً من ذلك.')
    });
  }

  publishExam(id: string): void {
    this.errorMessage = null;
    this.examService.publish(id).subscribe({
      next: () => this.load(),
      error: err => (this.errorMessage = (err.error?.errors ?? ['تعذّر نشر الامتحان.']).join(' ، '))
    });
  }

  closeExam(id: string): void {
    this.errorMessage = null;
    this.examService.close(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'تعذّر إغلاق الامتحان.')
    });
  }

  archiveExam(id: string): void {
    this.errorMessage = null;
    this.examService.archive(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'تعذّر أرشفة الامتحان.')
    });
  }
}
```

- [ ] **Step 2: Write `exams-list.component.html`**

```html
<div class="exams-page">
  <div class="header">
    <h2>إعدادات الامتحانات</h2>
    <button type="button" (click)="openCreateForm()">امتحان جديد</button>
  </div>

  <p class="error" *ngIf="errorMessage">{{ errorMessage }}</p>

  <table class="exams-table">
    <thead>
      <tr>
        <th>الاسم</th>
        <th>البداية</th>
        <th>النهاية</th>
        <th>عدد الأسئلة</th>
        <th>الدرجة الكلية</th>
        <th>الحالة</th>
        <th>إجراءات</th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let exam of exams()">
        <td>{{ exam.name }}</td>
        <td>{{ exam.startAtUtc | date: 'short' }}</td>
        <td>{{ exam.endAtUtc | date: 'short' }}</td>
        <td>{{ exam.totalQuestionCount }}</td>
        <td>{{ exam.totalPoints }}</td>
        <td>{{ exam.status }}</td>
        <td class="actions">
          <button type="button" (click)="openEditForm(exam.id)" [disabled]="exam.status !== 'Draft'">تعديل</button>
          <button type="button" (click)="publishExam(exam.id)" *ngIf="exam.status === 'Draft'">نشر</button>
          <button type="button" (click)="closeExam(exam.id)" *ngIf="exam.status === 'Published'">إغلاق</button>
          <button type="button" (click)="archiveExam(exam.id)" *ngIf="exam.status === 'Closed'">أرشفة</button>
          <button type="button" (click)="deleteExam(exam.id)" [disabled]="exam.status !== 'Draft'">حذف</button>
        </td>
      </tr>
    </tbody>
  </table>

  <div class="form-panel" *ngIf="isFormOpen()">
    <div class="form-panel-header">
      <h3>{{ editingExam() ? 'تعديل امتحان' : 'امتحان جديد' }}</h3>
      <button type="button" (click)="closeForm()">إغلاق</button>
    </div>
    <app-exam-form [topics]="topics()" [initialValue]="editingExam()" (save)="onSave($event)"></app-exam-form>
  </div>
</div>
```

- [ ] **Step 3: Wire up the route**

Modify `frontend/src/app/app.routes.ts` — add this entry inside the `admin` route's `children` array (after the `questions/import` entry):
```typescript
      {
        path: 'exams',
        loadComponent: () =>
          import('./features/admin/exams/exams-list.component').then(m => m.ExamsListComponent)
      }
```

- [ ] **Step 4: Manual verification**

Run the frontend dev server and the API, log in as Admin, navigate to `/admin/exams`:
- Create a Topic and a few Questions (Medium/Mcq, Hard/FillBlank) if none exist.
- Create an Exam selecting that topic with small counts (e.g. 1 Mcq/Medium).
- Confirm it appears in the list as `Draft` with the correct `totalQuestionCount`/`totalPoints`.
- Click "نشر" (Publish) — confirm it flips to `Published`.
- Try publishing an exam whose counts exceed the bank — confirm the error message lists the shortfall.
- Click "إغلاق" (Close) then "أرشفة" (Archive) — confirm status transitions and that Edit/Delete become disabled once not `Draft`.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/admin/exams/exams-list.component.ts frontend/src/app/features/admin/exams/exams-list.component.html frontend/src/app/app.routes.ts
git commit -m "feat(frontend): add exams-list admin page with lifecycle actions and routing"
```

---

### Task 11 (Could — optional, do last): Clone Exam (FR-4.10)

Lower priority than Tasks 1–10; skip if time-constrained, it does not block shipping the rest of FR-4.

**Files:**
- Create: `src/ExamSystem.Application/Features/Exams/CloneExam/CloneExamCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/CloneExam/CloneExamCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/CloneExamCommandHandlerTests.cs`
- Modify: `src/ExamSystem.Api/Controllers/ExamsController.cs`
- Modify: `frontend/src/app/core/services/exam.service.ts`
- Modify: `frontend/src/app/features/admin/exams/exams-list.component.ts` and `.html`

- [ ] **Step 1: Write the failing test**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/CloneExamCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.CloneExam;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class CloneExamCommandHandlerTests
{
    [Fact]
    public async Task Handle_PublishedExam_CreatesDraftCopyWithSameTopicSelections()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "Excel", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var original = new Exam { Name = "Original Exam", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddDays(7), DurationMinutes = 60, Status = ExamStatus.Published };
        original.TopicSelections.Add(new ExamTopicSelection { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        db.Exams.Add(original);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CloneExamCommandHandler(db);
        var result = await handler.Handle(new CloneExamCommand(original.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var clone = db.Exams.Include(e => e.TopicSelections).Single(e => e.Id == result.Value);
        Assert.Equal(ExamStatus.Draft, clone.Status);
        Assert.Equal("Original Exam (Copy)", clone.Name);
        Assert.Single(clone.TopicSelections);
        Assert.NotEqual(original.Id, clone.Id);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CloneExamCommandHandlerTests`
Expected: FAIL to compile — `CloneExamCommand`/`CloneExamCommandHandler` do not exist yet.

- [ ] **Step 3: Implement `CloneExamCommand` and handler**

`src/ExamSystem.Application/Features/Exams/CloneExam/CloneExamCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.CloneExam;

public record CloneExamCommand(Guid Id) : IRequest<Result<Guid>>;
```

`src/ExamSystem.Application/Features/Exams/CloneExam/CloneExamCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.CloneExam;

public class CloneExamCommandHandler : IRequestHandler<CloneExamCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CloneExamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CloneExamCommand request, CancellationToken cancellationToken)
    {
        var source = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (source is null)
        {
            return Result<Guid>.Failure("Exam not found.");
        }

        var clone = new Exam
        {
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            StartAtUtc = source.StartAtUtc,
            EndAtUtc = source.EndAtUtc,
            DurationMinutes = source.DurationMinutes,
            McqPoints = source.McqPoints,
            TrueFalsePoints = source.TrueFalsePoints,
            FillBlankPoints = source.FillBlankPoints,
            PassMarkPercentage = source.PassMarkPercentage,
            MaxAttempts = source.MaxAttempts,
            ShuffleAnswers = source.ShuffleAnswers,
            ShowResultImmediately = source.ShowResultImmediately,
            AllowBackNavigation = source.AllowBackNavigation,
            Status = ExamStatus.Draft
        };

        foreach (var selection in source.TopicSelections)
        {
            clone.TopicSelections.Add(new ExamTopicSelection
            {
                TopicId = selection.TopicId,
                DisplayOrder = selection.DisplayOrder,
                Difficulty = selection.Difficulty,
                Type = selection.Type,
                Count = selection.Count
            });
        }

        _db.Exams.Add(clone);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(clone.Id);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter CloneExamCommandHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 5: Add the endpoint**

Modify `src/ExamSystem.Api/Controllers/ExamsController.cs` — add this action (and its `using ExamSystem.Application.Features.Exams.CloneExam;`):
```csharp
    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CloneExamCommand(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { id = result.Value });
    }
```

- [ ] **Step 6: Add the frontend method and button**

Modify `frontend/src/app/core/services/exam.service.ts` — add:
```typescript
  clone(id: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/${id}/clone`, null);
  }
```

Modify `frontend/src/app/features/admin/exams/exams-list.component.ts` — add:
```typescript
  cloneExam(id: string): void {
    this.errorMessage = null;
    this.examService.clone(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'تعذّر استنساخ الامتحان.')
    });
  }
```

Modify `frontend/src/app/features/admin/exams/exams-list.component.html` — add a button inside the `<td class="actions">` cell:
```html
          <button type="button" (click)="cloneExam(exam.id)">استنساخ</button>
```

- [ ] **Step 7: Run the full backend test suite**

Run: `dotnet test`
Expected: all projects `Passed!`, 0 failures.

- [ ] **Step 8: Commit**

```bash
git add src/ExamSystem.Application src/ExamSystem.Api frontend/src/app/core/services/exam.service.ts frontend/src/app/features/admin/exams tests/ExamSystem.Application.UnitTests
git commit -m "feat: add CloneExam command, endpoint, and UI action (FR-4.10)"
```

---

## Spec Coverage Summary

| Requirement | Task |
|---|---|
| FR-4.1 (name/description/start-end) | Task 1, 3, 7, 9 |
| FR-4.2 (duration) | Task 1, 3, 7, 9 |
| FR-4.3 (topics + order + count per difficulty) | Task 1, 2, 3, 9 (extended with Type for FR-4.11/4.12) |
| FR-4.4 (grading config, per-question override reused from Phase 1a) | Task 1, 3, 9 — per-Type, see "Key design decision" above |
| FR-4.5 (pass mark %) | Task 1, 3, 9 |
| FR-4.6 (max attempts) | Task 1, 3, 9 |
| FR-4.7 (shuffle/show result/back navigation) | Task 1, 3, 9 |
| FR-4.8 (Draft→Published→Closed→Archived) | Task 1, 6, 7, 10 |
| FR-4.9 (publish validation against bank + dates) | Task 6 |
| FR-4.10 (Clone Exam) | Task 11 (Could, optional) |
| FR-4.11 (25 MCQ + 5 FillBlank, Medium+Hard default) | Achievable via Task 9's matrix — admin enters Medium/Mcq=25, Hard/FillBlank=5 |
| FR-4.12 (2 pts/MCQ, 5 pts/FillBlank, 75 total) | Task 1's default field values + Task 5's `TotalPoints` projection |
