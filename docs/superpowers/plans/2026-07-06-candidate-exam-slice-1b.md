# Candidate Exam — Slice 1b Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a candidate who has started an attempt take the exam end-to-end — answer questions with auto-save, resume, a server-authoritative timer with lazy auto-submit, then submit, auto-grade, and see the result.

**Architecture:** Clean Architecture + CQRS/MediatR mirroring the existing candidate features (Slice 1a). A new `AttemptAnswer` entity, an `IAttemptGradingService`, and four `AttemptToken`-authenticated endpoints under `CandidateAttemptController`. The frontend replaces the 1a attempt-shell placeholder with a one-question-per-screen player (timer, navigator, auto-save, result).

**Tech Stack:** .NET 8, EF Core 8 (SQL Server; SQLite for tests), MediatR, FluentValidation, xUnit; Angular 17 standalone components, SCSS.

**Spec:** `docs/superpowers/specs/2026-07-06-candidate-exam-slice-1b-design.md`
**Depends on:** Slice 1a (done) — `ExamAttempt`, `AttemptQuestion(+Option)`, `AttemptToken` scheme, candidate `/exam` area.

---

## Reference: existing types this plan uses (from Slice 1a)

- `ExamSystem.Domain.Attempts.ExamAttempt` — `Id, ExamId, CandidateId, StartedAtUtc, ExpiresAtUtc, SubmittedAtUtc?, Status (ExamAttemptStatus), Score?, Seed, ICollection<AttemptQuestion> Questions`.
- `ExamSystem.Domain.Attempts.AttemptQuestion` — `Id, AttemptId, SourceQuestionId, TopicId, DisplayOrder, Type (QuestionType), Difficulty, TextSnapshot, ImageUrlSnapshot?, CorrectAnswerTextSnapshot?, ICollection<AttemptQuestionOption> Options`.
- `ExamSystem.Domain.Attempts.AttemptQuestionOption` — `Id, AttemptQuestionId, TextSnapshot, IsCorrect, DisplayOrder`.
- `ExamSystem.Domain.Attempts.ExamAttemptStatus` — `InProgress, Submitted, AutoSubmitted, Terminated`.
- `ExamSystem.Domain.Questions.QuestionType` — `Mcq, TrueFalse, FillBlank`.
- `ExamSystem.Domain.Exams.Exam` — `McqPoints, TrueFalsePoints, FillBlankPoints (decimal), PassMarkPercentage (decimal), ShowResultImmediately (bool), DurationMinutes`.
- `ExamSystem.Infrastructure.Identity.AttemptTokenGenerator` — claim constants `AttemptIdClaim = "attempt_id"`, `CandidateIdClaim = "candidate_id"`, `ExamIdClaim = "exam_id"`.
- Frontend: `environment.apiBaseUrl` is `'/api'`; `attemptTokenInterceptor` attaches the stored token to `/api/exam/{id}/…`; `AttemptTokenStore` reads/writes `localStorage["attempt_{examId}"]`.

---

## File Structure

**Backend — Domain**
- `Attempts/AttemptAnswer.cs` (new)
- `Attempts/ExamAttempt.cs` (modify — add `Answers` navigation)
- `Questions/FillBlankAnswerRules.cs` (modify — add `Normalize`)

**Backend — Application**
- `Common/Interfaces/IApplicationDbContext.cs` (modify — add `DbSet<AttemptAnswer>`)
- `Common/Interfaces/IAttemptGradingService.cs` (new) + `Common/Models/GradeResult.cs` (new)
- `Features/CandidateExam/TakeExam/AttemptStateDto.cs`, `GetAttemptStateQuery.cs`, `GetAttemptStateQueryHandler.cs`
- `Features/CandidateExam/TakeExam/SaveAnswerCommand.cs`, `SaveAnswerCommandHandler.cs`
- `Features/CandidateExam/TakeExam/SubmitAttemptCommand.cs`, `SubmitAttemptCommandHandler.cs`, `ResultDto.cs`
- `Features/CandidateExam/TakeExam/GetResultQuery.cs`, `GetResultQueryHandler.cs`

**Backend — Infrastructure**
- `Persistence/ApplicationDbContext.cs` (modify — add `DbSet`)
- `Persistence/Configurations/AttemptAnswerConfiguration.cs` (new)
- `Grading/AttemptGradingService.cs` (new)
- `DependencyInjection.cs` (modify — register grading service)
- `Migrations/*` (generated)

**Backend — API**
- `Controllers/CandidateAttemptController.cs` (new)

**Backend — Tests**
- `tests/ExamSystem.Application.UnitTests/Grading/AttemptGradingServiceTests.cs`
- `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetAttemptStateQueryHandlerTests.cs`
- `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/SaveAnswerCommandHandlerTests.cs`
- `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/SubmitAttemptCommandHandlerTests.cs`
- `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateAttemptControllerTests.cs`

**Frontend**
- `core/services/candidate-attempt.service.ts` (+ `.spec.ts`)
- `features/candidate/attempt-player.component.ts` (+ `.html`, `.spec.ts`) — replaces attempt-shell
- `features/candidate/question-view.component.ts` (+ `.html`)
- `features/candidate/question-navigator.component.ts`
- `features/candidate/result.component.ts`
- `features/candidate/candidate.routes.ts` (modify — point `attempt` at the player)
- `features/candidate/attempt-shell.component.ts` (delete)
- `styles/_candidate.scss` (modify — player styles)

---

## Task 1: `AttemptAnswer` entity + FillBlank normalization

**Files:**
- Create: `src/ExamSystem.Domain/Attempts/AttemptAnswer.cs`
- Modify: `src/ExamSystem.Domain/Attempts/ExamAttempt.cs`
- Modify: `src/ExamSystem.Domain/Questions/FillBlankAnswerRules.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Domain/FillBlankNormalizeTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ExamSystem.Application.UnitTests/Domain/FillBlankNormalizeTests.cs`:

```csharp
using ExamSystem.Domain.Questions;
using Xunit;

namespace ExamSystem.Application.UnitTests.Domain;

public class FillBlankNormalizeTests
{
    [Theory]
    [InlineData("server", "server")]
    [InlineData("  Server ", "server")]
    [InlineData("SERVER", "server")]
    [InlineData("data base", "database")]
    [InlineData("Da Ta", "data")]
    public void Normalize_TrimsLowercasesAndStripsSpaces(string input, string expected)
    {
        Assert.Equal(expected, FillBlankAnswerRules.Normalize(input));
    }

    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FillBlankAnswerRules.Normalize(null));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~FillBlankNormalizeTests`
Expected: FAIL — `Normalize` does not exist.

- [ ] **Step 3: Add `Normalize` to `FillBlankAnswerRules`**

Modify `src/ExamSystem.Domain/Questions/FillBlankAnswerRules.cs` to:

```csharp
using System.Text.RegularExpressions;

namespace ExamSystem.Domain.Questions;

/// <summary>Single source of truth for the FillBlank single-lowercase-word rule (FR-3.2.1).</summary>
public static class FillBlankAnswerRules
{
    public static readonly Regex AnswerPattern = new("^[a-z0-9]+$");

    /// <summary>Normalizes a candidate's answer before comparison (FR-2 FillBlank rules):
    /// trim, lowercase (invariant), and remove all internal whitespace.</summary>
    public static string Normalize(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return string.Empty;
        }
        return new string(answer.Trim().ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
```

- [ ] **Step 4: Create the `AttemptAnswer` entity**

Create `src/ExamSystem.Domain/Attempts/AttemptAnswer.cs`:

```csharp
namespace ExamSystem.Domain.Attempts;

/// <summary>A candidate's answer to one snapshot question. IsCorrect is set at grading time.</summary>
public class AttemptAnswer : BaseEntity
{
    public Guid AttemptId { get; set; }
    public Guid AttemptQuestionId { get; set; }

    /// <summary>MCQ / TrueFalse: the chosen AttemptQuestionOption.Id.</summary>
    public Guid? SelectedOptionId { get; set; }

    /// <summary>FillBlank: the raw text the candidate typed.</summary>
    public string? AnswerText { get; set; }

    public bool IsFlagged { get; set; }
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAtUtc { get; set; }
}
```

- [ ] **Step 5: Add the `Answers` navigation to `ExamAttempt`**

Modify `src/ExamSystem.Domain/Attempts/ExamAttempt.cs` — add this property after `Questions`:

```csharp
    public ICollection<AttemptAnswer> Answers { get; set; } = new List<AttemptAnswer>();
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~FillBlankNormalizeTests`
Expected: PASS (6 cases).

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Domain/Attempts/AttemptAnswer.cs src/ExamSystem.Domain/Attempts/ExamAttempt.cs src/ExamSystem.Domain/Questions/FillBlankAnswerRules.cs tests/ExamSystem.Application.UnitTests/Domain/FillBlankNormalizeTests.cs
git commit -m "feat(domain): add AttemptAnswer entity and FillBlank answer normalization"
```

---

## Task 2: Persistence — DbSet, configuration, migration

**Files:**
- Modify: `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/AttemptAnswerConfiguration.cs`

- [ ] **Step 1: Add the DbSet to the interface**

Modify `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs` — add after `DbSet<AttemptQuestionOption> AttemptQuestionOptions { get; }`:

```csharp
    DbSet<AttemptAnswer> AttemptAnswers { get; }
```

- [ ] **Step 2: Add the DbSet to the context**

Modify `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs` — add after the `AttemptQuestionOptions` property:

```csharp
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
```

- [ ] **Step 3: Write the configuration**

Create `src/ExamSystem.Infrastructure/Persistence/Configurations/AttemptAnswerConfiguration.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamSystem.Infrastructure.Persistence.Configurations;

public class AttemptAnswerConfiguration : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.Property(a => a.AnswerText).HasMaxLength(50);
        builder.HasIndex(a => new { a.AttemptId, a.AttemptQuestionId }).IsUnique();

        builder.HasOne<ExamAttempt>()
            .WithMany(e => e.Answers)
            .HasForeignKey(a => a.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build ExamSystem.sln`
Expected: Build succeeded.

- [ ] **Step 5: Generate the migration**

Run:
```bash
dotnet ef migrations add AddAttemptAnswers \
  --project src/ExamSystem.Infrastructure \
  --startup-project src/ExamSystem.Api \
  --output-dir Migrations
```
Expected: `Migrations/*_AddAttemptAnswers.cs` created; open it and confirm it creates the `AttemptAnswers` table with the unique index on `(AttemptId, AttemptQuestionId)`.

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs src/ExamSystem.Infrastructure/Persistence/ src/ExamSystem.Infrastructure/Migrations/
git commit -m "feat(infra): persist AttemptAnswer + migration"
```

---

## Task 3: `IAttemptGradingService`

**Files:**
- Create: `src/ExamSystem.Application/Common/Models/GradeResult.cs`
- Create: `src/ExamSystem.Application/Common/Interfaces/IAttemptGradingService.cs`
- Create: `src/ExamSystem.Infrastructure/Grading/AttemptGradingService.cs`
- Modify: `src/ExamSystem.Infrastructure/DependencyInjection.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Grading/AttemptGradingServiceTests.cs`

- [ ] **Step 1: Define the result model + interface**

Create `src/ExamSystem.Application/Common/Models/GradeResult.cs`:

```csharp
namespace ExamSystem.Application.Common.Models;

public record GradeResult(decimal Score, decimal TotalPoints, decimal PassMarkPercentage, bool Passed);
```

Create `src/ExamSystem.Application/Common/Interfaces/IAttemptGradingService.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Grades an attempt's answers against its immutable snapshot (FR-2.13). Mutates each
/// AttemptAnswer.IsCorrect and returns the totals. Points come from the exam's per-type values.
/// The attempt must be loaded with Questions (+Options) and Answers.
/// </summary>
public interface IAttemptGradingService
{
    GradeResult Grade(ExamAttempt attempt, Exam exam);
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/Grading/AttemptGradingServiceTests.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Grading;
using Xunit;

namespace ExamSystem.Application.UnitTests.Grading;

public class AttemptGradingServiceTests
{
    private static Exam Exam() => new()
    {
        McqPoints = 2m, TrueFalsePoints = 1m, FillBlankPoints = 5m, PassMarkPercentage = 60m
    };

    private static (AttemptQuestion q, Guid correctOptionId) McqQuestion(int order)
    {
        var correct = new AttemptQuestionOption { TextSnapshot = "right", IsCorrect = true, DisplayOrder = 1 };
        var wrong = new AttemptQuestionOption { TextSnapshot = "wrong", IsCorrect = false, DisplayOrder = 2 };
        var q = new AttemptQuestion
        {
            DisplayOrder = order, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
            TextSnapshot = "q", Options = new List<AttemptQuestionOption> { correct, wrong }
        };
        correct.AttemptQuestionId = q.Id; wrong.AttemptQuestionId = q.Id;
        return (q, correct.Id);
    }

    private static AttemptQuestion FillBlankQuestion(int order, string answer)
    {
        return new AttemptQuestion
        {
            DisplayOrder = order, Type = QuestionType.FillBlank, Difficulty = DifficultyLevel.Hard,
            TextSnapshot = "fb", CorrectAnswerTextSnapshot = answer
        };
    }

    [Fact]
    public void Grade_AllCorrect_ScoresFullAndPasses()
    {
        var (mcq, correctId) = McqQuestion(1);
        var fb = FillBlankQuestion(2, "server");
        var attempt = new ExamAttempt { Questions = { mcq, fb } };
        attempt.Answers.Add(new AttemptAnswer { AttemptQuestionId = mcq.Id, SelectedOptionId = correctId });
        attempt.Answers.Add(new AttemptAnswer { AttemptQuestionId = fb.Id, AnswerText = " SERVER " });

        var result = new AttemptGradingService().Grade(attempt, Exam());

        Assert.Equal(7m, result.Score);        // 2 + 5
        Assert.Equal(7m, result.TotalPoints);
        Assert.True(result.Passed);
        Assert.All(attempt.Answers, a => Assert.True(a.IsCorrect));
    }

    [Fact]
    public void Grade_MixedAndUnanswered_ScoresPartialAndFails()
    {
        var (mcq, correctId) = McqQuestion(1);
        var fb = FillBlankQuestion(2, "server");
        var attempt = new ExamAttempt { Questions = { mcq, fb } };
        // MCQ answered wrong; FillBlank unanswered
        var wrongOptionId = mcq.Options.First(o => !o.IsCorrect).Id;
        attempt.Answers.Add(new AttemptAnswer { AttemptQuestionId = mcq.Id, SelectedOptionId = wrongOptionId });

        var result = new AttemptGradingService().Grade(attempt, Exam());

        Assert.Equal(0m, result.Score);
        Assert.Equal(7m, result.TotalPoints);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Grade_FillBlankCaseAndSpaceInsensitive()
    {
        var fb = FillBlankQuestion(1, "database");
        var attempt = new ExamAttempt { Questions = { fb } };
        attempt.Answers.Add(new AttemptAnswer { AttemptQuestionId = fb.Id, AnswerText = "Data Base" });

        var result = new AttemptGradingService().Grade(attempt, Exam());

        Assert.Equal(5m, result.Score);
        Assert.True(attempt.Answers.Single().IsCorrect);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~AttemptGradingServiceTests`
Expected: FAIL — `AttemptGradingService` does not exist.

- [ ] **Step 4: Implement the grading service**

Create `src/ExamSystem.Infrastructure/Grading/AttemptGradingService.cs`:

```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Common.Models;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;

namespace ExamSystem.Infrastructure.Grading;

public class AttemptGradingService : IAttemptGradingService
{
    public GradeResult Grade(ExamAttempt attempt, Exam exam)
    {
        var answersByQuestion = attempt.Answers.ToDictionary(a => a.AttemptQuestionId);
        decimal score = 0m;
        decimal total = 0m;

        foreach (var question in attempt.Questions)
        {
            var points = PointsFor(question.Type, exam);
            total += points;

            if (!answersByQuestion.TryGetValue(question.Id, out var answer))
            {
                continue; // unanswered -> wrong, contributes 0
            }

            var correct = question.Type == QuestionType.FillBlank
                ? FillBlankAnswerRules.Normalize(answer.AnswerText) == (question.CorrectAnswerTextSnapshot ?? string.Empty)
                : answer.SelectedOptionId is { } optionId
                  && question.Options.Any(o => o.Id == optionId && o.IsCorrect);

            answer.IsCorrect = correct;
            if (correct)
            {
                score += points;
            }
        }

        var passed = total > 0m && score / total * 100m >= exam.PassMarkPercentage;
        return new GradeResult(score, total, exam.PassMarkPercentage, passed);
    }

    private static decimal PointsFor(QuestionType type, Exam exam) => type switch
    {
        QuestionType.Mcq => exam.McqPoints,
        QuestionType.TrueFalse => exam.TrueFalsePoints,
        QuestionType.FillBlank => exam.FillBlankPoints,
        _ => 0m
    };
}
```

- [ ] **Step 5: Register the service**

Modify `src/ExamSystem.Infrastructure/DependencyInjection.cs` — add the using:

```csharp
using ExamSystem.Infrastructure.Grading;
```

Add before `return services;`:

```csharp
        services.AddScoped<IAttemptGradingService, AttemptGradingService>();
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~AttemptGradingServiceTests`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application/Common/Models/GradeResult.cs src/ExamSystem.Application/Common/Interfaces/IAttemptGradingService.cs src/ExamSystem.Infrastructure/Grading/ src/ExamSystem.Infrastructure/DependencyInjection.cs tests/ExamSystem.Application.UnitTests/Grading/
git commit -m "feat: attempt grading service (MCQ + FillBlank, per-type points, pass/fail)"
```

---

## Task 4: `GetAttemptStateQuery` (sanitized delivery + lazy auto-submit)

**Files:**
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/AttemptStateDto.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetAttemptStateQuery.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetAttemptStateQueryHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetAttemptStateQueryHandlerTests.cs`

- [ ] **Step 1: Write the DTOs + query**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/AttemptStateDto.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record AttemptOptionDto(Guid Id, string Text);

public record AttemptQuestionStateDto(
    Guid AttemptQuestionId,
    int DisplayOrder,
    string Type,
    string Text,
    string? ImageUrl,
    IReadOnlyList<AttemptOptionDto> Options,
    Guid? SelectedOptionId,
    string? AnswerText,
    bool IsFlagged);

public record AttemptStateDto(
    string Status,
    int RemainingSeconds,
    bool ShowResultImmediately,
    IReadOnlyList<AttemptQuestionStateDto> Questions);
```

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetAttemptStateQuery.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record GetAttemptStateQuery(Guid AttemptId) : IRequest<Result<AttemptStateDto>>;
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetAttemptStateQueryHandlerTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Grading;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class GetAttemptStateQueryHandlerTests
{
    private static async Task<(Guid attemptId, Guid examId)> SeedInProgressAsync(
        Infrastructure.Persistence.ApplicationDbContext db, DateTime expiresAtUtc)
    {
        var exam = new Exam { Name = "E", DurationMinutes = 60, ShowResultImmediately = true,
            McqPoints = 2m, PassMarkPercentage = 60m };
        db.Exams.Add(exam);
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10), ExpiresAtUtc = expiresAtUtc,
            Status = ExamAttemptStatus.InProgress
        };
        var opt1 = new AttemptQuestionOption { TextSnapshot = "A", IsCorrect = true, DisplayOrder = 1 };
        var opt2 = new AttemptQuestionOption { TextSnapshot = "B", IsCorrect = false, DisplayOrder = 2 };
        var q = new AttemptQuestion
        {
            AttemptId = attempt.Id, DisplayOrder = 1, Type = QuestionType.Mcq,
            Difficulty = DifficultyLevel.Medium, TextSnapshot = "Q1",
            Options = new List<AttemptQuestionOption> { opt1, opt2 }
        };
        attempt.Questions.Add(q);
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        return (attempt.Id, exam.Id);
    }

    [Fact]
    public async Task Handle_InProgress_ReturnsSanitizedQuestionsWithoutCorrectness()
    {
        using var db = TestDbContextFactory.Create();
        var (attemptId, _) = await SeedInProgressAsync(db, DateTime.UtcNow.AddMinutes(30));

        var handler = new GetAttemptStateQueryHandler(db, new AttemptGradingService());
        var result = await handler.Handle(new GetAttemptStateQuery(attemptId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("InProgress", result.Value!.Status);
        Assert.True(result.Value.RemainingSeconds > 0);
        var q = Assert.Single(result.Value.Questions);
        Assert.Equal(2, q.Options.Count);
        // sanitized: options expose only id + text (no correctness), and no correct-answer field exists on the DTO
        Assert.All(q.Options, o => Assert.False(string.IsNullOrEmpty(o.Text)));
    }

    [Fact]
    public async Task Handle_Expired_LazyAutoSubmits()
    {
        using var db = TestDbContextFactory.Create();
        var (attemptId, _) = await SeedInProgressAsync(db, DateTime.UtcNow.AddMinutes(-1));

        var handler = new GetAttemptStateQueryHandler(db, new AttemptGradingService());
        var result = await handler.Handle(new GetAttemptStateQuery(attemptId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AutoSubmitted", result.Value!.Status);
        Assert.Equal(0, result.Value.RemainingSeconds);
        var saved = db.ExamAttempts.Single();
        Assert.Equal(ExamAttemptStatus.AutoSubmitted, saved.Status);
        Assert.NotNull(saved.SubmittedAtUtc);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~GetAttemptStateQueryHandlerTests`
Expected: FAIL — handler does not exist.

- [ ] **Step 4: Implement the handler**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetAttemptStateQueryHandler.cs`:

```csharp
using ExamSystem.Domain.Attempts;

namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class GetAttemptStateQueryHandler : IRequestHandler<GetAttemptStateQuery, Result<AttemptStateDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IAttemptGradingService _grading;

    public GetAttemptStateQueryHandler(IApplicationDbContext db, IAttemptGradingService grading)
    {
        _db = db;
        _grading = grading;
    }

    public async Task<Result<AttemptStateDto>> Handle(GetAttemptStateQuery request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<AttemptStateDto>.Failure("Attempt not found.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<AttemptStateDto>.Failure("Exam not found.");
        }

        var now = DateTime.UtcNow;
        if (attempt.Status == ExamAttemptStatus.InProgress && now > attempt.ExpiresAtUtc)
        {
            var grade = _grading.Grade(attempt, exam);
            attempt.Score = grade.Score;
            attempt.Status = ExamAttemptStatus.AutoSubmitted;
            attempt.SubmittedAtUtc = attempt.ExpiresAtUtc;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var remaining = attempt.Status == ExamAttemptStatus.InProgress
            ? Math.Max(0, (int)(attempt.ExpiresAtUtc - now).TotalSeconds)
            : 0;

        var answersByQuestion = attempt.Answers.ToDictionary(a => a.AttemptQuestionId);
        var questions = attempt.Questions
            .OrderBy(q => q.DisplayOrder)
            .Select(q =>
            {
                answersByQuestion.TryGetValue(q.Id, out var answer);
                return new AttemptQuestionStateDto(
                    q.Id, q.DisplayOrder, q.Type.ToString(), q.TextSnapshot, q.ImageUrlSnapshot,
                    q.Options.OrderBy(o => o.DisplayOrder)
                        .Select(o => new AttemptOptionDto(o.Id, o.TextSnapshot)).ToList(),
                    answer?.SelectedOptionId, answer?.AnswerText, answer?.IsFlagged ?? false);
            })
            .ToList();

        return Result<AttemptStateDto>.Success(new AttemptStateDto(
            attempt.Status.ToString(), remaining, exam.ShowResultImmediately, questions));
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~GetAttemptStateQueryHandlerTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/TakeExam/AttemptStateDto.cs src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetAttemptState* tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetAttemptStateQueryHandlerTests.cs
git commit -m "feat(app): GetAttemptState with sanitized delivery and lazy auto-submit"
```

---

## Task 5: `SaveAnswerCommand`

**Files:**
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/SaveAnswerCommand.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/SaveAnswerCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/SaveAnswerCommandHandlerTests.cs`

- [ ] **Step 1: Write the command**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/SaveAnswerCommand.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record SaveAnswerCommand(
    Guid AttemptId,
    Guid AttemptQuestionId,
    Guid? SelectedOptionId,
    string? AnswerText,
    bool IsFlagged) : IRequest<Result<bool>>;
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/SaveAnswerCommandHandlerTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Grading;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class SaveAnswerCommandHandlerTests
{
    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, ExamAttempt attempt, AttemptQuestion q, Guid optId)>
        SeedAsync(DateTime expiresAtUtc)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, McqPoints = 2m, PassMarkPercentage = 60m };
        db.Exams.Add(exam);
        var opt = new AttemptQuestionOption { TextSnapshot = "A", IsCorrect = true, DisplayOrder = 1 };
        var q = new AttemptQuestion
        {
            DisplayOrder = 1, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
            TextSnapshot = "Q", Options = new List<AttemptQuestionOption> { opt }
        };
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5), ExpiresAtUtc = expiresAtUtc,
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Questions.Add(q);
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, attempt, q, opt.Id);
    }

    [Fact]
    public async Task Handle_ValidMcqAnswer_UpsertsAnswer()
    {
        var (db, attempt, q, optId) = await SeedAsync(DateTime.UtcNow.AddMinutes(30));
        var handler = new SaveAnswerCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(
            new SaveAnswerCommand(attempt.Id, q.Id, optId, null, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = db.AttemptAnswers.Single();
        Assert.Equal(optId, saved.SelectedOptionId);

        // upsert: saving again updates the same row (flag it)
        await handler.Handle(new SaveAnswerCommand(attempt.Id, q.Id, optId, null, true), CancellationToken.None);
        Assert.Single(db.AttemptAnswers);
        Assert.True(db.AttemptAnswers.Single().IsFlagged);
    }

    [Fact]
    public async Task Handle_OptionNotInQuestion_Fails()
    {
        var (db, attempt, q, _) = await SeedAsync(DateTime.UtcNow.AddMinutes(30));
        var handler = new SaveAnswerCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(
            new SaveAnswerCommand(attempt.Id, q.Id, Guid.NewGuid(), null, false), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ExpiredAttempt_AutoSubmitsAndFails()
    {
        var (db, attempt, q, optId) = await SeedAsync(DateTime.UtcNow.AddMinutes(-1));
        var handler = new SaveAnswerCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(
            new SaveAnswerCommand(attempt.Id, q.Id, optId, null, false), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExamAttemptStatus.AutoSubmitted, db.ExamAttempts.Single().Status);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~SaveAnswerCommandHandlerTests`
Expected: FAIL — handler does not exist.

- [ ] **Step 4: Implement the handler**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/SaveAnswerCommandHandler.cs`:

```csharp
using ExamSystem.Domain.Attempts;

namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class SaveAnswerCommandHandler : IRequestHandler<SaveAnswerCommand, Result<bool>>
{
    private readonly IApplicationDbContext _db;
    private readonly IAttemptGradingService _grading;

    public SaveAnswerCommandHandler(IApplicationDbContext db, IAttemptGradingService grading)
    {
        _db = db;
        _grading = grading;
    }

    public async Task<Result<bool>> Handle(SaveAnswerCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<bool>.Failure("Attempt not found.");
        }

        if (attempt.Status != ExamAttemptStatus.InProgress)
        {
            return Result<bool>.Failure("This attempt is closed.");
        }

        if (DateTime.UtcNow > attempt.ExpiresAtUtc)
        {
            var exam = await _db.Exams.FirstAsync(e => e.Id == attempt.ExamId, cancellationToken);
            var grade = _grading.Grade(attempt, exam);
            attempt.Score = grade.Score;
            attempt.Status = ExamAttemptStatus.AutoSubmitted;
            attempt.SubmittedAtUtc = attempt.ExpiresAtUtc;
            await _db.SaveChangesAsync(cancellationToken);
            return Result<bool>.Failure("Time is up; the exam was submitted automatically.");
        }

        var question = attempt.Questions.FirstOrDefault(q => q.Id == request.AttemptQuestionId);
        if (question is null)
        {
            return Result<bool>.Failure("Question is not part of this attempt.");
        }

        if (request.SelectedOptionId is { } optionId && question.Options.All(o => o.Id != optionId))
        {
            return Result<bool>.Failure("Selected option is not valid for this question.");
        }

        if (request.AnswerText is { Length: > 50 })
        {
            return Result<bool>.Failure("Answer is too long.");
        }

        var answer = attempt.Answers.FirstOrDefault(a => a.AttemptQuestionId == request.AttemptQuestionId);
        if (answer is null)
        {
            answer = new AttemptAnswer { AttemptId = attempt.Id, AttemptQuestionId = request.AttemptQuestionId };
            attempt.Answers.Add(answer);
        }
        answer.SelectedOptionId = request.SelectedOptionId;
        answer.AnswerText = request.AnswerText;
        answer.IsFlagged = request.IsFlagged;
        answer.AnsweredAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~SaveAnswerCommandHandlerTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/TakeExam/SaveAnswer* tests/ExamSystem.Application.UnitTests/Features/CandidateExam/SaveAnswerCommandHandlerTests.cs
git commit -m "feat(app): SaveAnswer upsert with validation and expiry auto-submit"
```

---

## Task 6: `SubmitAttemptCommand` + `GetResultQuery`

**Files:**
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/ResultDto.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/SubmitAttemptCommand.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/SubmitAttemptCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetResultQuery.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetResultQueryHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/SubmitAttemptCommandHandlerTests.cs`

- [ ] **Step 1: Write the DTO + command + query**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/ResultDto.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

/// <summary>When Shown is false the score fields are withheld (Exam.ShowResultImmediately == false).</summary>
public record ResultDto(bool Shown, decimal Score, decimal TotalPoints, decimal PassMarkPercentage, bool Passed);
```

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/SubmitAttemptCommand.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record SubmitAttemptCommand(Guid AttemptId) : IRequest<Result<ResultDto>>;
```

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetResultQuery.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public record GetResultQuery(Guid AttemptId) : IRequest<Result<ResultDto>>;
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/SubmitAttemptCommandHandlerTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Infrastructure.Grading;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class SubmitAttemptCommandHandlerTests
{
    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, ExamAttempt attempt)>
        SeedAnsweredAsync(bool showResult)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, McqPoints = 2m, PassMarkPercentage = 60m,
            ShowResultImmediately = showResult };
        db.Exams.Add(exam);
        var opt = new AttemptQuestionOption { TextSnapshot = "A", IsCorrect = true, DisplayOrder = 1 };
        var q = new AttemptQuestion
        {
            DisplayOrder = 1, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
            TextSnapshot = "Q", Options = new List<AttemptQuestionOption> { opt }
        };
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Questions.Add(q);
        attempt.Answers.Add(new AttemptAnswer { AttemptId = attempt.Id, AttemptQuestionId = q.Id, SelectedOptionId = opt.Id });
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, attempt);
    }

    [Fact]
    public async Task Handle_Submit_GradesAndMarksSubmitted()
    {
        var (db, attempt) = await SeedAnsweredAsync(showResult: true);
        var handler = new SubmitAttemptCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(new SubmitAttemptCommand(attempt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Shown);
        Assert.Equal(2m, result.Value.Score);
        Assert.True(result.Value.Passed);
        Assert.Equal(ExamAttemptStatus.Submitted, db.ExamAttempts.Single().Status);
    }

    [Fact]
    public async Task Handle_SubmitTwice_IsIdempotent()
    {
        var (db, attempt) = await SeedAnsweredAsync(showResult: true);
        var handler = new SubmitAttemptCommandHandler(db, new AttemptGradingService());

        var first = await handler.Handle(new SubmitAttemptCommand(attempt.Id), CancellationToken.None);
        var submittedAt = db.ExamAttempts.Single().SubmittedAtUtc;
        var second = await handler.Handle(new SubmitAttemptCommand(attempt.Id), CancellationToken.None);

        Assert.Equal(first.Value!.Score, second.Value!.Score);
        Assert.Equal(submittedAt, db.ExamAttempts.Single().SubmittedAtUtc);
    }

    [Fact]
    public async Task Handle_ResultWithheld_WhenShowResultImmediatelyFalse()
    {
        var (db, attempt) = await SeedAnsweredAsync(showResult: false);
        var handler = new SubmitAttemptCommandHandler(db, new AttemptGradingService());

        var result = await handler.Handle(new SubmitAttemptCommand(attempt.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Shown);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~SubmitAttemptCommandHandlerTests`
Expected: FAIL — handler does not exist.

- [ ] **Step 4: Implement the submit handler**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/SubmitAttemptCommandHandler.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class SubmitAttemptCommandHandler : IRequestHandler<SubmitAttemptCommand, Result<ResultDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IAttemptGradingService _grading;

    public SubmitAttemptCommandHandler(IApplicationDbContext db, IAttemptGradingService grading)
    {
        _db = db;
        _grading = grading;
    }

    public async Task<Result<ResultDto>> Handle(SubmitAttemptCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions).ThenInclude(q => q.Options)
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<ResultDto>.Failure("Attempt not found.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<ResultDto>.Failure("Exam not found.");
        }

        if (attempt.Status == ExamAttemptStatus.InProgress)
        {
            var expired = DateTime.UtcNow > attempt.ExpiresAtUtc;
            var grade = _grading.Grade(attempt, exam);
            attempt.Score = grade.Score;
            attempt.Status = expired ? ExamAttemptStatus.AutoSubmitted : ExamAttemptStatus.Submitted;
            attempt.SubmittedAtUtc = expired ? attempt.ExpiresAtUtc : DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result<ResultDto>.Success(BuildResult(attempt, exam));
    }

    internal static ResultDto BuildResult(ExamAttempt attempt, Exam exam)
    {
        var total = attempt.Questions.Sum(q => q.Type switch
        {
            Domain.Questions.QuestionType.Mcq => exam.McqPoints,
            Domain.Questions.QuestionType.TrueFalse => exam.TrueFalsePoints,
            Domain.Questions.QuestionType.FillBlank => exam.FillBlankPoints,
            _ => 0m
        });
        var score = attempt.Score ?? 0m;
        var passed = total > 0m && score / total * 100m >= exam.PassMarkPercentage;
        return new ResultDto(exam.ShowResultImmediately, score, total, exam.PassMarkPercentage, passed);
    }
}
```

- [ ] **Step 5: Implement the result query handler**

Create `src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetResultQueryHandler.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.TakeExam;

public class GetResultQueryHandler : IRequestHandler<GetResultQuery, Result<ResultDto>>
{
    private readonly IApplicationDbContext _db;

    public GetResultQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ResultDto>> Handle(GetResultQuery request, CancellationToken cancellationToken)
    {
        var attempt = await _db.ExamAttempts
            .Include(a => a.Questions)
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId, cancellationToken);
        if (attempt is null)
        {
            return Result<ResultDto>.Failure("Attempt not found.");
        }

        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == attempt.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<ResultDto>.Failure("Exam not found.");
        }

        return Result<ResultDto>.Success(SubmitAttemptCommandHandler.BuildResult(attempt, exam));
    }
}
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~SubmitAttemptCommandHandlerTests`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/TakeExam/ResultDto.cs src/ExamSystem.Application/Features/CandidateExam/TakeExam/SubmitAttempt* src/ExamSystem.Application/Features/CandidateExam/TakeExam/GetResult* tests/ExamSystem.Application.UnitTests/Features/CandidateExam/SubmitAttemptCommandHandlerTests.cs
git commit -m "feat(app): submit + grade (idempotent) and result query with visibility gate"
```

---

## Task 7: `CandidateAttemptController` + integration tests

**Files:**
- Create: `src/ExamSystem.Api/Controllers/CandidateAttemptController.cs`
- Test: `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateAttemptControllerTests.cs`

- [ ] **Step 1: Write the controller**

Create `src/ExamSystem.Api/Controllers/CandidateAttemptController.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.TakeExam;
using ExamSystem.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Candidate exam engine (Slice 1b), authenticated by the AttemptToken scheme.</summary>
[ApiController]
[Route("api/exam/{examId:guid}/attempt")]
[Authorize(AuthenticationSchemes = "AttemptToken")]
public class CandidateAttemptController : ControllerBase
{
    private readonly ISender _sender;

    public CandidateAttemptController(ISender sender) => _sender = sender;

    // Resolves the attempt id from the token and enforces it belongs to the route's exam.
    private IActionResult? Resolve(Guid examId, out Guid attemptId)
    {
        attemptId = Guid.Empty;
        var attemptClaim = User.FindFirst(AttemptTokenGenerator.AttemptIdClaim)?.Value;
        var examClaim = User.FindFirst(AttemptTokenGenerator.ExamIdClaim)?.Value;
        if (!Guid.TryParse(attemptClaim, out attemptId) || !Guid.TryParse(examClaim, out var tokenExamId)
            || tokenExamId != examId)
        {
            return Forbid();
        }
        return null;
    }

    [HttpGet("state")]
    public async Task<IActionResult> State(Guid examId, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        var result = await _sender.Send(new GetAttemptStateQuery(attemptId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { errors = result.Errors });
    }

    [HttpPost("answer")]
    public async Task<IActionResult> Answer(Guid examId, [FromBody] SaveAnswerRequest request, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        var result = await _sender.Send(
            new SaveAnswerCommand(attemptId, request.AttemptQuestionId, request.SelectedOptionId, request.AnswerText, request.IsFlagged),
            cancellationToken);
        return result.IsSuccess ? NoContent() : Conflict(new { errors = result.Errors });
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(Guid examId, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        var result = await _sender.Send(new SubmitAttemptCommand(attemptId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("result")]
    public async Task<IActionResult> Result(Guid examId, CancellationToken cancellationToken)
    {
        if (Resolve(examId, out var attemptId) is { } forbid) return forbid;
        var result = await _sender.Send(new GetResultQuery(attemptId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { errors = result.Errors });
    }

    public record SaveAnswerRequest(Guid AttemptQuestionId, Guid? SelectedOptionId, string? AnswerText, bool IsFlagged);
}
```

- [ ] **Step 2: Write the failing integration tests**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateAttemptControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class CandidateAttemptControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public CandidateAttemptControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    private static object Identity => new { fullName = "احمد محمد علي حسن", nationalId = "29912310123404", mobileNumber = "01012345678" };

    // Admin creates a published, open exam with 2 MCQ questions and a selection of 2.
    private static async Task<Guid> CreatePublishedExamAsync(HttpClient admin)
    {
        var topicResp = await admin.PostAsJsonAsync("/api/admin/topics", new { name = $"T{Guid.NewGuid():N}", displayOrder = 1 });
        topicResp.EnsureSuccessStatusCode();
        var topicId = (await topicResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        for (var i = 0; i < 2; i++)
        {
            (await admin.PostAsJsonAsync("/api/admin/questions", new
            {
                topicId, type = "Mcq", difficulty = "Medium", text = $"Q{i}",
                options = new[] { new { text = "wrong", isCorrect = false }, new { text = "right", isCorrect = true } }
            })).EnsureSuccessStatusCode();
        }
        var examResp = await admin.PostAsJsonAsync("/api/admin/exams", new
        {
            name = $"Exam {Guid.NewGuid():N}", description = (string?)null,
            startAtUtc = DateTime.UtcNow.AddMinutes(-5), endAtUtc = DateTime.UtcNow.AddHours(2),
            durationMinutes = 60, mcqPoints = 2m, trueFalsePoints = 1m, fillBlankPoints = 5m,
            passMarkPercentage = 60m, maxAttempts = 1, shuffleAnswers = true,
            showResultImmediately = true, allowBackNavigation = true,
            topicSelections = new[] { new { topicId, displayOrder = 1, difficulty = "Medium", type = "Mcq", count = 2 } }
        });
        examResp.EnsureSuccessStatusCode();
        var examId = (await examResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        (await admin.PostAsync($"/api/admin/exams/{examId}/publish", null)).EnsureSuccessStatusCode();
        return examId;
    }

    private async Task<(HttpClient client, Guid examId)> StartAttemptAsync()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        var anon = _factory.CreateClient();
        var startResp = await anon.PostAsJsonAsync($"/api/exam/{examId}/start", Identity);
        startResp.EnsureSuccessStatusCode();
        var start = await startResp.Content.ReadFromJsonAsync<StartResponse>();
        anon.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", start!.AttemptToken);
        return (anon, examId);
    }

    [Fact]
    public async Task State_ReturnsSanitizedQuestions_NoCorrectnessLeak()
    {
        var (client, examId) = await StartAttemptAsync();

        var resp = await client.GetAsync($"/api/exam/{examId}/attempt/state");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var raw = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("isCorrect", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctAnswer", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Answer_ThenSubmit_GradesCorrectly()
    {
        var (client, examId) = await StartAttemptAsync();
        var state = await (await client.GetAsync($"/api/exam/{examId}/attempt/state")).Content.ReadFromJsonAsync<StateResponse>();

        foreach (var q in state!.Questions)
        {
            var right = q.Options.First(o => o.Text == "right").Id;
            var save = await client.PostAsJsonAsync($"/api/exam/{examId}/attempt/answer",
                new { attemptQuestionId = q.AttemptQuestionId, selectedOptionId = right, answerText = (string?)null, isFlagged = false });
            Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        }

        var submit = await client.PostAsync($"/api/exam/{examId}/attempt/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var result = await submit.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.True(result!.Shown);
        Assert.Equal(4m, result.Score);   // 2 questions x 2 points
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task State_WithoutToken_IsUnauthorized()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        var anon = _factory.CreateClient(); // no attempt token

        var resp = await anon.GetAsync($"/api/exam/{examId}/attempt/state");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private record IdResponse(Guid Id);
    private record StartResponse(Guid AttemptId, string AttemptToken, DateTime ExpiresAtUtc);
    private record OptionResponse(Guid Id, string Text);
    private record QuestionResponse(Guid AttemptQuestionId, int DisplayOrder, string Type, string Text, string? ImageUrl,
        List<OptionResponse> Options, Guid? SelectedOptionId, string? AnswerText, bool IsFlagged);
    private record StateResponse(string Status, int RemainingSeconds, bool ShowResultImmediately, List<QuestionResponse> Questions);
    private record ResultResponse(bool Shown, decimal Score, decimal TotalPoints, decimal PassMarkPercentage, bool Passed);
}
```

- [ ] **Step 3: Run to verify it fails, then passes**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter FullyQualifiedName~CandidateAttemptControllerTests`
Expected: after the controller from Step 1 is in place, PASS (3 tests). (If run before the controller compiles, it fails to route → 404/Unauthorized mismatch.)

- [ ] **Step 4: Run the full backend suite**

Run: `dotnet test ExamSystem.sln`
Expected: all tests pass (1a + 1b).

- [ ] **Step 5: Commit**

```bash
git add src/ExamSystem.Api/Controllers/CandidateAttemptController.cs tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateAttemptControllerTests.cs
git commit -m "feat(api): AttemptToken-scoped exam engine endpoints (state, answer, submit, result)"
```

---

## Task 8: Frontend `CandidateAttemptService`

**Files:**
- Create: `frontend/src/app/core/services/candidate-attempt.service.ts`
- Test: `frontend/src/app/core/services/candidate-attempt.service.spec.ts`

- [ ] **Step 1: Write the service**

Create `frontend/src/app/core/services/candidate-attempt.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AttemptOption { id: string; text: string; }
export interface AttemptQuestionState {
  attemptQuestionId: string;
  displayOrder: number;
  type: 'Mcq' | 'TrueFalse' | 'FillBlank';
  text: string;
  imageUrl: string | null;
  options: AttemptOption[];
  selectedOptionId: string | null;
  answerText: string | null;
  isFlagged: boolean;
}
export interface AttemptState {
  status: 'InProgress' | 'Submitted' | 'AutoSubmitted' | 'Terminated';
  remainingSeconds: number;
  showResultImmediately: boolean;
  questions: AttemptQuestionState[];
}
export interface SaveAnswerPayload {
  attemptQuestionId: string;
  selectedOptionId: string | null;
  answerText: string | null;
  isFlagged: boolean;
}
export interface AttemptResult {
  shown: boolean;
  score: number;
  totalPoints: number;
  passMarkPercentage: number;
  passed: boolean;
}

@Injectable({ providedIn: 'root' })
export class CandidateAttemptService {
  private base(examId: string): string { return `${environment.apiBaseUrl}/exam/${examId}/attempt`; }

  constructor(private readonly http: HttpClient) {}

  state(examId: string): Observable<AttemptState> {
    return this.http.get<AttemptState>(`${this.base(examId)}/state`);
  }
  saveAnswer(examId: string, payload: SaveAnswerPayload): Observable<void> {
    return this.http.post<void>(`${this.base(examId)}/answer`, payload);
  }
  submit(examId: string): Observable<AttemptResult> {
    return this.http.post<AttemptResult>(`${this.base(examId)}/submit`, {});
  }
  result(examId: string): Observable<AttemptResult> {
    return this.http.get<AttemptResult>(`${this.base(examId)}/result`);
  }
}
```

- [ ] **Step 2: Write the failing spec**

Create `frontend/src/app/core/services/candidate-attempt.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CandidateAttemptService } from './candidate-attempt.service';
import { environment } from '../../../environments/environment';

describe('CandidateAttemptService', () => {
  let service: CandidateAttemptService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [CandidateAttemptService] });
    service = TestBed.inject(CandidateAttemptService);
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  it('posts an answer to the attempt answer endpoint', () => {
    const examId = 'e1';
    service.saveAnswer(examId, { attemptQuestionId: 'q1', selectedOptionId: 'o1', answerText: null, isFlagged: false })
      .subscribe();
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/exam/${examId}/attempt/answer`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.attemptQuestionId).toBe('q1');
    req.flush(null);
  });

  it('submits the attempt and returns the result', () => {
    const examId = 'e1';
    let passed: boolean | undefined;
    service.submit(examId).subscribe(r => (passed = r.passed));
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/exam/${examId}/attempt/submit`);
    req.flush({ shown: true, score: 4, totalPoints: 4, passMarkPercentage: 60, passed: true });
    expect(passed).toBeTrue();
  });
});
```

- [ ] **Step 3: Run to verify it passes**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/candidate-attempt.service.spec.ts'`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/app/core/services/candidate-attempt.service.ts frontend/src/app/core/services/candidate-attempt.service.spec.ts
git commit -m "feat(candidate-ui): candidate attempt service (state, answer, submit, result)"
```

---

## Task 9: Question view + navigator components (dumb)

**Files:**
- Create: `frontend/src/app/features/candidate/question-view.component.ts` (+ `.html`)
- Create: `frontend/src/app/features/candidate/question-navigator.component.ts`
- Modify: `frontend/src/styles/_candidate.scss`

- [ ] **Step 1: Write the question view component**

Create `frontend/src/app/features/candidate/question-view.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { AttemptQuestionState } from '../../core/services/candidate-attempt.service';

@Component({
  selector: 'app-question-view',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './question-view.component.html'
})
export class QuestionViewComponent {
  @Input({ required: true }) question!: AttemptQuestionState;
  @Output() optionSelected = new EventEmitter<string>();
  @Output() textChanged = new EventEmitter<string>();

  onText(value: string): void {
    // FillBlank rule: lowercase, no spaces, max 50 (server enforces too)
    const clean = value.toLowerCase().replace(/\s+/g, '').slice(0, 50);
    this.question.answerText = clean;
    this.textChanged.emit(clean);
  }
}
```

- [ ] **Step 2: Write the question view template**

Create `frontend/src/app/features/candidate/question-view.component.html`:

```html
<div class="q-view">
  <p class="q-text">{{ question.displayOrder }}. {{ question.text }}</p>
  <img *ngIf="question.imageUrl" [src]="question.imageUrl" alt="" class="q-image" />

  <ng-container *ngIf="question.type !== 'FillBlank'; else fillBlank">
    <label class="q-option" *ngFor="let opt of question.options"
           [class.selected]="question.selectedOptionId === opt.id">
      <input type="radio" [name]="question.attemptQuestionId" [value]="opt.id"
             [checked]="question.selectedOptionId === opt.id"
             (change)="optionSelected.emit(opt.id)" />
      <span>{{ opt.text }}</span>
    </label>
  </ng-container>

  <ng-template #fillBlank>
    <input class="q-fill" type="text" inputmode="text" maxlength="50"
           [value]="question.answerText || ''"
           (input)="onText($any($event.target).value)"
           placeholder="اكتب الإجابة بكلمة واحدة" />
  </ng-template>
</div>
```

- [ ] **Step 3: Write the navigator component**

Create `frontend/src/app/features/candidate/question-navigator.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { AttemptQuestionState } from '../../core/services/candidate-attempt.service';

@Component({
  selector: 'app-question-navigator',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="q-nav">
      <button type="button" class="q-chip"
              *ngFor="let q of questions; let i = index"
              [class.answered]="isAnswered(q)"
              [class.flagged]="q.isFlagged"
              [class.current]="i === currentIndex"
              (click)="jump.emit(i)">
        {{ i + 1 }}
      </button>
    </div>
  `
})
export class QuestionNavigatorComponent {
  @Input({ required: true }) questions!: AttemptQuestionState[];
  @Input() currentIndex = 0;
  @Output() jump = new EventEmitter<number>();

  isAnswered(q: AttemptQuestionState): boolean {
    return !!q.selectedOptionId || !!(q.answerText && q.answerText.length > 0);
  }
}
```

- [ ] **Step 4: Add player styles**

Append to `frontend/src/styles/_candidate.scss`:

```scss
// --- Exam player -------------------------------------------------------
.q-view { display: flex; flex-direction: column; gap: var(--space-md); }
.q-text { font-size: var(--fs-title); font-weight: var(--fw-semibold); line-height: 1.7; }
.q-image { max-width: 100%; border-radius: var(--radius-md); }

.q-option {
  display: flex; align-items: center; gap: var(--space-sm);
  padding: 12px var(--space-md); border: 1px solid var(--hairline-grey);
  border-radius: var(--radius-md); cursor: pointer;
  transition: border-color var(--dur-fast) var(--ease-out), background-color var(--dur-fast) var(--ease-out);
}
.q-option.selected { border-color: var(--judicial-green); background: #eaf3f0; }
.q-option input { accent-color: var(--judicial-green); }

.q-fill {
  width: 100%; padding: 12px var(--space-md);
  border: 1px solid var(--hairline-grey); border-radius: var(--radius-md);
  font-size: var(--fs-title); text-align: center;
}

.q-nav { display: flex; flex-wrap: wrap; gap: var(--space-xs); justify-content: center; }
.q-chip {
  width: 38px; height: 38px; padding: 0; border-radius: var(--radius-md);
  border: 1px solid var(--hairline-grey); background: var(--pure-white);
  color: var(--candidate-ink); font-weight: var(--fw-semibold); cursor: pointer;
}
.q-chip.answered { border-color: var(--judicial-green); color: var(--judicial-green); }
.q-chip.flagged { border-color: var(--caution-amber); }
.q-chip.current { background: var(--judicial-green); color: var(--pure-white); border-color: var(--judicial-green); }

.player-bar { display: flex; align-items: center; justify-content: space-between; gap: var(--space-md); }
.player-timer { font-variant-numeric: tabular-nums; font-weight: var(--fw-bold); }
.player-timer.low { color: var(--alert-red); }
.player-actions { display: flex; gap: var(--space-sm); }
.btn-ghost {
  background: var(--pure-white); color: var(--judicial-green);
  border: 1px solid var(--judicial-green); border-radius: var(--radius-md);
  padding: 10px 18px; font-weight: var(--fw-semibold); cursor: pointer;
}
.btn-ghost:disabled { opacity: 0.5; cursor: not-allowed; }
.result-score { font-size: var(--fs-display); font-weight: var(--fw-bold); }
.result-pass { color: var(--confirm-green); }
.result-fail { color: var(--alert-red); }
```

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/candidate/question-view.component.* frontend/src/app/features/candidate/question-navigator.component.ts frontend/src/styles/_candidate.scss
git commit -m "feat(candidate-ui): question view and navigator components + player styles"
```

---

## Task 10: `ResultComponent` + `AttemptPlayerComponent` + route swap

**Files:**
- Create: `frontend/src/app/features/candidate/result.component.ts`
- Create: `frontend/src/app/features/candidate/attempt-player.component.ts` (+ `.html`)
- Create: `frontend/src/app/features/candidate/attempt-player.component.spec.ts`
- Modify: `frontend/src/app/features/candidate/candidate.routes.ts`
- Delete: `frontend/src/app/features/candidate/attempt-shell.component.ts`

- [ ] **Step 1: Write the result component**

Create `frontend/src/app/features/candidate/result.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { AttemptResult } from '../../core/services/candidate-attempt.service';

@Component({
  selector: 'app-result',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="candidate-card">
      <div class="candidate-header"><div class="exam-title">نتيجة الامتحان</div></div>
      <div class="candidate-state" *ngIf="result">
        <ng-container *ngIf="result.shown; else withheld">
          <p class="result-score">{{ result.score }} / {{ result.totalPoints }}</p>
          <p [class.result-pass]="result.passed" [class.result-fail]="!result.passed">
            {{ result.passed ? 'ناجح' : 'راسب' }} (درجة النجاح {{ result.passMarkPercentage }}%)
          </p>
        </ng-container>
        <ng-template #withheld>
          <p>تم تسليم امتحانك بنجاح. ستُعلن النتيجة لاحقاً.</p>
        </ng-template>
      </div>
    </div>
  `
})
export class ResultComponent {
  @Input({ required: true }) result!: AttemptResult;
}
```

- [ ] **Step 2: Write the player component**

Create `frontend/src/app/features/candidate/attempt-player.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  AttemptResult, AttemptState, AttemptQuestionState, CandidateAttemptService
} from '../../core/services/candidate-attempt.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';
import { QuestionViewComponent } from './question-view.component';
import { QuestionNavigatorComponent } from './question-navigator.component';
import { ResultComponent } from './result.component';

@Component({
  selector: 'app-attempt-player',
  standalone: true,
  imports: [CommonModule, QuestionViewComponent, QuestionNavigatorComponent, ResultComponent],
  templateUrl: './attempt-player.component.html'
})
export class AttemptPlayerComponent implements OnInit, OnDestroy {
  examId = '';
  loading = true;
  state: AttemptState | null = null;
  currentIndex = 0;
  remainingSeconds = 0;
  submitting = false;
  confirming = false;
  result: AttemptResult | null = null;

  private timer?: ReturnType<typeof setInterval>;
  private readonly textSave$ = new Subject<AttemptQuestionState>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: CandidateAttemptService,
    private readonly tokenStore: AttemptTokenStore
  ) {}

  get current(): AttemptQuestionState | null { return this.state?.questions[this.currentIndex] ?? null; }
  get unansweredCount(): number {
    return this.state?.questions.filter(q => !q.selectedOptionId && !(q.answerText && q.answerText.length)).length ?? 0;
  }

  ngOnInit(): void {
    this.examId = this.route.parent?.snapshot.paramMap.get('examId') ?? '';
    if (!this.tokenStore.get(this.examId)) {
      this.router.navigate(['../'], { relativeTo: this.route });
      return;
    }
    this.textSave$.pipe(debounceTime(1000)).subscribe(q => this.persist(q));
    this.service.state(this.examId).subscribe({
      next: s => this.applyState(s),
      error: () => this.router.navigate(['../'], { relativeTo: this.route })
    });
  }

  ngOnDestroy(): void {
    if (this.timer) { clearInterval(this.timer); }
  }

  private applyState(s: AttemptState): void {
    this.loading = false;
    this.state = s;
    if (s.status !== 'InProgress') { this.loadResult(); return; }
    this.remainingSeconds = s.remainingSeconds;
    this.startTimer();
  }

  private startTimer(): void {
    this.timer = setInterval(() => {
      this.remainingSeconds = Math.max(0, this.remainingSeconds - 1);
      if (this.remainingSeconds === 0) { clearInterval(this.timer); this.doSubmit(); }
    }, 1000);
  }

  selectOption(optionId: string): void {
    if (!this.current) { return; }
    this.current.selectedOptionId = optionId;
    this.persist(this.current);
  }

  changeText(): void {
    if (this.current) { this.textSave$.next(this.current); }
  }

  toggleFlag(): void {
    if (!this.current) { return; }
    this.current.isFlagged = !this.current.isFlagged;
    this.persist(this.current);
  }

  private persist(q: AttemptQuestionState): void {
    this.service.saveAnswer(this.examId, {
      attemptQuestionId: q.attemptQuestionId,
      selectedOptionId: q.selectedOptionId,
      answerText: q.answerText,
      isFlagged: q.isFlagged
    }).subscribe({ error: () => this.doSubmit() }); // a 409 means time is up
  }

  go(index: number): void { this.currentIndex = index; }
  prev(): void { if (this.currentIndex > 0) { this.currentIndex--; } }
  next(): void { if (this.state && this.currentIndex < this.state.questions.length - 1) { this.currentIndex++; } }

  askConfirm(): void { this.confirming = true; }
  cancelConfirm(): void { this.confirming = false; }

  doSubmit(): void {
    if (this.submitting) { return; }
    this.submitting = true;
    if (this.timer) { clearInterval(this.timer); }
    this.service.submit(this.examId).subscribe({
      next: r => { this.result = r; this.confirming = false; },
      error: () => this.loadResult()
    });
  }

  private loadResult(): void {
    this.service.result(this.examId).subscribe({ next: r => (this.result = r) });
  }
}
```

- [ ] **Step 3: Write the player template**

Create `frontend/src/app/features/candidate/attempt-player.component.html`:

```html
<app-result *ngIf="result" [result]="result"></app-result>

<div class="candidate-card" *ngIf="!result">
  <div class="candidate-state" *ngIf="loading">جارٍ التحميل…</div>

  <ng-container *ngIf="!loading && state && current">
    <div class="player-bar">
      <span class="player-timer" [class.low]="remainingSeconds < 60">
        {{ (remainingSeconds / 60) | number: '1.0-0' }}:{{ (remainingSeconds % 60) | number: '2.0-0' }}
      </span>
      <button type="button" class="btn-ghost" (click)="toggleFlag()">
        {{ current.isFlagged ? 'إلغاء التعليم' : 'تعليم للمراجعة' }}
      </button>
    </div>

    <app-question-view [question]="current"
                       (optionSelected)="selectOption($event)"
                       (textChanged)="changeText()"></app-question-view>

    <div class="player-bar">
      <div class="player-actions">
        <button type="button" class="btn-ghost" (click)="prev()" [disabled]="currentIndex === 0">السابق</button>
        <button type="button" class="btn-ghost" (click)="next()"
                [disabled]="currentIndex === state.questions.length - 1">التالي</button>
      </div>
      <button type="button" class="btn-primary" style="width:auto" (click)="askConfirm()">تسليم</button>
    </div>

    <app-question-navigator [questions]="state.questions" [currentIndex]="currentIndex"
                            (jump)="go($event)"></app-question-navigator>
  </ng-container>

  <div class="candidate-state" *ngIf="confirming">
    <p>لديك <strong>{{ unansweredCount }}</strong> سؤالاً بدون إجابة. هل تريد التسليم النهائي؟</p>
    <button type="button" class="btn-primary" (click)="doSubmit()" [disabled]="submitting">
      {{ submitting ? 'جارٍ التسليم…' : 'تأكيد التسليم' }}
    </button>
    <button type="button" class="btn-ghost" (click)="cancelConfirm()">رجوع</button>
  </div>
</div>
```

- [ ] **Step 4: Point the route at the player and delete the shell**

Modify `frontend/src/app/features/candidate/candidate.routes.ts` — change the `attempt` child to:

```typescript
      {
        path: 'attempt',
        loadComponent: () => import('./attempt-player.component').then(m => m.AttemptPlayerComponent)
      }
```

Delete `frontend/src/app/features/candidate/attempt-shell.component.ts`:

```bash
git rm frontend/src/app/features/candidate/attempt-shell.component.ts
```

- [ ] **Step 5: Write the player spec**

Create `frontend/src/app/features/candidate/attempt-player.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { AttemptPlayerComponent } from './attempt-player.component';
import { CandidateAttemptService, AttemptState } from '../../core/services/candidate-attempt.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';

describe('AttemptPlayerComponent', () => {
  const state: AttemptState = {
    status: 'InProgress', remainingSeconds: 300, showResultImmediately: true,
    questions: [
      { attemptQuestionId: 'q1', displayOrder: 1, type: 'Mcq', text: 'Q1', imageUrl: null,
        options: [{ id: 'o1', text: 'A' }, { id: 'o2', text: 'B' }],
        selectedOptionId: null, answerText: null, isFlagged: false }
    ]
  };
  const serviceStub = {
    state: () => of(state),
    saveAnswer: jasmine.createSpy('saveAnswer').and.returnValue(of(void 0)),
    submit: () => of({ shown: true, score: 2, totalPoints: 2, passMarkPercentage: 60, passed: true }),
    result: () => of({ shown: true, score: 2, totalPoints: 2, passMarkPercentage: 60, passed: true })
  };
  const tokenStub = { get: () => 'tok', set: () => {}, clear: () => {} };

  let fixture: ComponentFixture<AttemptPlayerComponent>;
  let component: AttemptPlayerComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AttemptPlayerComponent],
      providers: [
        { provide: CandidateAttemptService, useValue: serviceStub },
        { provide: AttemptTokenStore, useValue: tokenStub },
        { provide: ActivatedRoute, useValue: { parent: { snapshot: { paramMap: new Map([['examId', 'e1']]) } } } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(AttemptPlayerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the attempt state and shows the first question', () => {
    expect(component.state?.questions.length).toBe(1);
    expect(component.current?.attemptQuestionId).toBe('q1');
  });

  it('saves an answer immediately when an option is selected', () => {
    component.selectOption('o1');
    expect(serviceStub.saveAnswer).toHaveBeenCalled();
    expect(component.current?.selectedOptionId).toBe('o1');
  });

  it('reports the unanswered count', () => {
    expect(component.unansweredCount).toBe(1);
    component.selectOption('o1');
    expect(component.unansweredCount).toBe(0);
  });
});
```

- [ ] **Step 6: Build + run the full frontend suite**

Run: `cd frontend && npm run build`
Expected: Build succeeds (the `attempt` route now resolves to the player; the shell is gone).

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: PASS (existing + new player/service specs).

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/candidate/ frontend/src/app/features/candidate/candidate.routes.ts
git commit -m "feat(candidate-ui): exam player (timer, autosave, navigator, submit) and result screen"
```

---

## Task 11: End-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Run both servers**

Ensure the API and frontend dev servers are running (the API applies the `AddAttemptAnswers` migration on startup; the frontend proxy is already configured).

- [ ] **Step 2: Drive the full candidate flow (real data)**

Create/publish a currently-open exam with a small MCQ bank (reuse the seed approach from 1a). In the browser:
- `/exam/{examId}` → register → instructions → "ابدأ الامتحان" → the **player** loads (question, timer counting down, navigator).
- Select an option → confirm via `preview_network` that `POST .../attempt/answer` returns `204`.
- Click a navigator chip → the question changes; the answered chip is green.
- "تسليم" → confirmation shows the unanswered count → "تأكيد التسليم" → `POST .../attempt/submit` returns `200` and the result screen shows score + pass/fail.
- Confirm via `preview_network` that the `state` response contains **no** `isCorrect` / `correctAnswer` fields.

- [ ] **Step 3: Screenshot proof**

Capture `preview_screenshot` of the player and the result screen. Confirm no `preview_console_logs` errors.

- [ ] **Step 4: Final full test run**

Run: `dotnet test ExamSystem.sln` and `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: all green.

---

## Self-Review — Spec Coverage

- Spec §2 in-scope (state, autosave MCQ/FillBlank/flag, one-per-screen + navigator, timer + lazy auto-submit, submit confirm + grade, result gated by ShowResultImmediately, resume) → Tasks 4-10. ✅
- Spec §3.1 Domain (`AttemptAnswer`, `Answers` nav, `Normalize`) → Task 1. ✅
- Spec §3.2 Application (GetAttemptState, SaveAnswer, SubmitAttempt, GetResult, `IAttemptGradingService`) → Tasks 3-6. ✅
- Spec §3.3 Infrastructure (config + migration + grading DI) → Tasks 2, 3. ✅
- Spec §3.4 API (`CandidateAttemptController`, 4 endpoints, AttemptToken + claim/exam match) → Task 7. ✅
- Spec §4 Frontend (player, question view, navigator, submit confirm, result, service, autosave cadence, resume) → Tasks 8-10. ✅
- Spec §5 error handling (expired→auto-submit, already-submitted idempotent, invalid option/overlong, token missing→redirect, unanswered allowed) → Tasks 4-7, 10. ✅
- Spec §6 testing (unit grading/save/state-sanitization/submit-idempotency/lazy-auto-submit; integration full flow + sanitization + no-token 401; frontend load/autosave/navigator/unanswered/result) → every backend task + Tasks 8, 10. ✅

No placeholders remain. Type/name usage is consistent across tasks (`AttemptStateDto`, `AttemptQuestionStateDto`, `SaveAnswerCommand`, `ResultDto`, `IAttemptGradingService.Grade`, `AttemptTokenGenerator.AttemptIdClaim`, service `state/saveAnswer/submit/result`).

**Deferred (per spec §7):** per-question points override (snapshot change), TrueFalse UI, synonyms — intentionally not in this plan.
