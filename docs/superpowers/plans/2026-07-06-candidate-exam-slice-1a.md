# Candidate Exam — Slice 1a Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a candidate open a public exam link, register with validated identity, and start an attempt (server-side timer + immutable question snapshot + attempt token) — up to the exam-player shell.

**Architecture:** Clean Architecture + CQRS/MediatR mirroring the existing admin features. New Domain entities (`Candidate`, `ExamAttempt`, `AttemptQuestion`, `AttemptQuestionOption`, `CandidateExamAttemptGrant`) and a `NationalId` value object; three anonymous endpoints under `CandidateExamController`; a second JWT scheme (`AttemptToken`) issued at start. Frontend adds a public `/exam` area (outside the admin auth guard) with its own calm candidate theme.

**Tech Stack:** .NET 8, EF Core 8 (SQL Server; SQLite for tests), MediatR, FluentValidation, xUnit; Angular 17 standalone components, SCSS.

**Spec:** `docs/superpowers/specs/2026-07-06-candidate-exam-slice-1a-design.md`

---

## File Structure

**Backend — Domain (`src/ExamSystem.Domain`)**
- `Candidates/Candidate.cs`, `Candidates/Gender.cs`, `Candidates/NationalId.cs`, `Candidates/CandidateExamAttemptGrant.cs`
- `Attempts/ExamAttempt.cs`, `Attempts/ExamAttemptStatus.cs`, `Attempts/AttemptQuestion.cs`, `Attempts/AttemptQuestionOption.cs`

**Backend — Application (`src/ExamSystem.Application`)**
- `Common/Interfaces/IApplicationDbContext.cs` (modify — add DbSets)
- `Common/Interfaces/IAttemptTokenGenerator.cs`
- `Common/Interfaces/IQuestionSelectionService.cs`
- `Features/CandidateExam/GetExamLanding/GetExamLandingQuery.cs` (+ `Handler`, `ExamLandingDto`)
- `Features/CandidateExam/RegisterCandidate/RegisterCandidateCommand.cs` (+ `Handler`, `Validator`, `RegisterResultDto`)
- `Features/CandidateExam/StartAttempt/StartAttemptCommand.cs` (+ `Handler`, `Validator`, `StartAttemptDto`)

**Backend — Infrastructure (`src/ExamSystem.Infrastructure`)**
- `Persistence/ApplicationDbContext.cs` (modify — add DbSets)
- `Persistence/Configurations/CandidateConfiguration.cs`, `CandidateExamAttemptGrantConfiguration.cs`, `ExamAttemptConfiguration.cs`, `AttemptQuestionConfiguration.cs`, `AttemptQuestionOptionConfiguration.cs`
- `Selection/QuestionSelectionService.cs`
- `Identity/AttemptTokenSettings.cs`, `Identity/AttemptTokenGenerator.cs`
- `DependencyInjection.cs` (modify — register the two services)
- `Migrations/*` (generated)

**Backend — API (`src/ExamSystem.Api`)**
- `Controllers/CandidateExamController.cs`
- `Program.cs` (modify — register the `AttemptToken` JWT scheme + options)

**Backend — Tests**
- `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/*`, `.../Domain/NationalIdTests.cs`, `.../Selection/QuestionSelectionServiceTests.cs`
- `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateExamControllerTests.cs`

**Frontend (`frontend/src/app`)**
- `features/candidate/candidate.routes.ts`
- `features/candidate/candidate-layout.component.ts`
- `features/candidate/exam-landing.component.ts` (+ `.html`, `.spec.ts`)
- `features/candidate/instructions.component.ts` (+ `.html`)
- `features/candidate/attempt-shell.component.ts` (1a placeholder)
- `core/services/candidate-exam.service.ts` (+ `.spec.ts`)
- `core/services/attempt-token.store.ts`
- `core/interceptors/attempt-token.interceptor.ts`
- `styles/_candidate.scss`, `styles.scss` (modify — import), `app.routes.ts` (modify), `app.config.ts` (modify), `assets/moj-logo.png` (asset)

---

## Task 1: `NationalId` value object (validation + derivation)

**Files:**
- Create: `src/ExamSystem.Domain/Candidates/Gender.cs`
- Create: `src/ExamSystem.Domain/Candidates/NationalId.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Domain/NationalIdTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/Domain/NationalIdTests.cs`:

```csharp
using ExamSystem.Domain.Candidates;
using Xunit;

namespace ExamSystem.Application.UnitTests.Domain;

public class NationalIdTests
{
    // 2 99 12 31 01 2345 -> century 1900s, born 1999-12-31, Cairo(01), serial ...4 -> even -> Female
    private const string Valid1999Female = "29912310123454";
    // 3 01 06 15 21 1235 -> century 2000s, born 2001-06-15, Giza(21), serial ...3 -> odd -> Male
    private const string Valid2001Male = "30106152112354";

    [Fact]
    public void TryParse_ValidId_DerivesBirthDateGenderAndGovernorate()
    {
        var ok = NationalId.TryParse(Valid1999Female, out var id, out var error);

        Assert.True(ok, error);
        Assert.Equal(new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc), id!.BirthDateUtc);
        Assert.Equal(Gender.Female, id.Gender);
        Assert.Equal(1, id.GovernorateCode);
        Assert.Equal(Valid1999Female, id.Value);
    }

    [Fact]
    public void TryParse_MaleOddSerial_DerivesMale()
    {
        Assert.True(NationalId.TryParse(Valid2001Male, out var id, out _));
        Assert.Equal(Gender.Male, id!.Gender);
        Assert.Equal(new DateTime(2001, 6, 15, 0, 0, 0, DateTimeKind.Utc), id.BirthDateUtc);
        Assert.Equal(21, id.GovernorateCode);
    }

    [Theory]
    [InlineData("", "National ID must be exactly 14 digits.")]
    [InlineData("123", "National ID must be exactly 14 digits.")]
    [InlineData("2991231012345X", "National ID must be exactly 14 digits.")]
    [InlineData("19912310123454", "National ID has an invalid century digit.")]      // century 1
    [InlineData("29913310123454", "National ID contains an invalid birth date.")]     // month 13
    [InlineData("29912319923454", "National ID has an invalid governorate code.")]     // gov 99
    public void TryParse_InvalidId_FailsWithMessage(string value, string expected)
    {
        var ok = NationalId.TryParse(value, out var id, out var error);

        Assert.False(ok);
        Assert.Null(id);
        Assert.Equal(expected, error);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~NationalIdTests`
Expected: FAIL — `NationalId`/`Gender` do not exist (compile error).

- [ ] **Step 3: Implement `Gender` and `NationalId`**

Create `src/ExamSystem.Domain/Candidates/Gender.cs`:

```csharp
namespace ExamSystem.Domain.Candidates;

public enum Gender
{
    Male = 1,
    Female = 2
}
```

Create `src/ExamSystem.Domain/Candidates/NationalId.cs`:

```csharp
using System.Globalization;

namespace ExamSystem.Domain.Candidates;

/// <summary>
/// Egyptian National ID value object. Structure: C YYMMDD GG NNNN S
/// (century, birth date, governorate, serial, check digit). We validate structure only
/// (FR-1.2): 14 digits, century 2 or 3, a real birth date, and a known governorate code.
/// The check digit is not algorithmically verified in v1.
/// </summary>
public sealed class NationalId
{
    public string Value { get; }
    public DateTime BirthDateUtc { get; }
    public Gender Gender { get; }
    public int GovernorateCode { get; }

    private NationalId(string value, DateTime birthDateUtc, Gender gender, int governorateCode)
    {
        Value = value;
        BirthDateUtc = birthDateUtc;
        Gender = gender;
        GovernorateCode = governorateCode;
    }

    public static bool TryParse(string? input, out NationalId? id, out string? error)
    {
        id = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input) || input.Length != 14 || !input.All(char.IsDigit))
        {
            error = "National ID must be exactly 14 digits.";
            return false;
        }

        var century = input[0] switch { '2' => 1900, '3' => 2000, _ => -1 };
        if (century == -1)
        {
            error = "National ID has an invalid century digit.";
            return false;
        }

        var year = century + int.Parse(input.Substring(1, 2), CultureInfo.InvariantCulture);
        var month = int.Parse(input.Substring(3, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(input.Substring(5, 2), CultureInfo.InvariantCulture);
        if (!TryBuildDate(year, month, day, out var birthDate))
        {
            error = "National ID contains an invalid birth date.";
            return false;
        }

        var governorate = int.Parse(input.Substring(7, 2), CultureInfo.InvariantCulture);
        if (!((governorate >= 1 && governorate <= 35) || governorate == 88))
        {
            error = "National ID has an invalid governorate code.";
            return false;
        }

        var genderDigit = input[12] - '0';
        var gender = genderDigit % 2 == 1 ? Gender.Male : Gender.Female;

        id = new NationalId(input, birthDate, gender, governorate);
        return true;
    }

    private static bool TryBuildDate(int year, int month, int day, out DateTime date)
    {
        date = default;
        if (month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }
        date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        return true;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~NationalIdTests`
Expected: PASS (5 test cases).

- [ ] **Step 5: Commit**

```bash
git add src/ExamSystem.Domain/Candidates/ tests/ExamSystem.Application.UnitTests/Domain/NationalIdTests.cs
git commit -m "feat(domain): add NationalId value object with structural validation and derivation"
```

---

## Task 2: Candidate & Attempt entities

**Files:**
- Create: `src/ExamSystem.Domain/Candidates/Candidate.cs`
- Create: `src/ExamSystem.Domain/Candidates/CandidateExamAttemptGrant.cs`
- Create: `src/ExamSystem.Domain/Attempts/ExamAttemptStatus.cs`
- Create: `src/ExamSystem.Domain/Attempts/ExamAttempt.cs`
- Create: `src/ExamSystem.Domain/Attempts/AttemptQuestion.cs`
- Create: `src/ExamSystem.Domain/Attempts/AttemptQuestionOption.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Domain/EntityDefaultsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ExamSystem.Application.UnitTests/Domain/EntityDefaultsTests.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using Xunit;

namespace ExamSystem.Application.UnitTests.Domain;

public class EntityDefaultsTests
{
    [Fact]
    public void ExamAttempt_Defaults_AreInProgressWithEmptyCollections()
    {
        var attempt = new ExamAttempt();

        Assert.Equal(ExamAttemptStatus.InProgress, attempt.Status);
        Assert.NotEqual(System.Guid.Empty, attempt.Id);
        Assert.Empty(attempt.Questions);
    }

    [Fact]
    public void Candidate_Defaults_HaveGeneratedIdAndEmptyGrants()
    {
        var candidate = new Candidate();

        Assert.NotEqual(System.Guid.Empty, candidate.Id);
        Assert.Empty(candidate.Grants);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~EntityDefaultsTests`
Expected: FAIL — entities do not exist.

- [ ] **Step 3: Implement the entities**

Create `src/ExamSystem.Domain/Candidates/Candidate.cs`:

```csharp
namespace ExamSystem.Domain.Candidates;

/// <summary>Permanent candidate profile, keyed by national ID and reused across exams (FR-1.5).</summary>
public class Candidate : BaseAuditableEntity
{
    public string NationalId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;

    public DateTime BirthDateUtc { get; set; }
    public Gender Gender { get; set; }
    public int GovernorateCode { get; set; }

    public ICollection<CandidateExamAttemptGrant> Grants { get; set; } = new List<CandidateExamAttemptGrant>();
}
```

Create `src/ExamSystem.Domain/Candidates/CandidateExamAttemptGrant.cs`:

```csharp
namespace ExamSystem.Domain.Candidates;

/// <summary>
/// Admin re-activation record (FR-5.4): an active grant lets a candidate exceed the
/// one-attempt-per-exam limit for a specific exam. Read-only in Slice 1a (written by admin later).
/// </summary>
public class CandidateExamAttemptGrant : BaseAuditableEntity
{
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public Guid ExamId { get; set; }
    public bool IsActive { get; set; } = true;
}
```

Create `src/ExamSystem.Domain/Attempts/ExamAttemptStatus.cs`:

```csharp
namespace ExamSystem.Domain.Attempts;

public enum ExamAttemptStatus
{
    InProgress = 0,
    Submitted = 1,
    AutoSubmitted = 2,
    Terminated = 3
}
```

Create `src/ExamSystem.Domain/Attempts/ExamAttempt.cs`:

```csharp
namespace ExamSystem.Domain.Attempts;

public class ExamAttempt : BaseAuditableEntity
{
    public Guid ExamId { get; set; }
    public Guid CandidateId { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }

    public ExamAttemptStatus Status { get; set; } = ExamAttemptStatus.InProgress;
    public decimal? Score { get; set; }

    /// <summary>Seed for deterministic question selection/shuffle (== Id); enables audit replay (FR-2 note).</summary>
    public int Seed { get; set; }

    public ICollection<AttemptQuestion> Questions { get; set; } = new List<AttemptQuestion>();
}
```

Create `src/ExamSystem.Domain/Attempts/AttemptQuestion.cs`:

```csharp
using ExamSystem.Domain.Questions;

namespace ExamSystem.Domain.Attempts;

/// <summary>Immutable per-attempt snapshot of a presented question (FR-2.5).</summary>
public class AttemptQuestion : BaseEntity
{
    public Guid AttemptId { get; set; }
    public Guid SourceQuestionId { get; set; }
    public Guid TopicId { get; set; }

    public int DisplayOrder { get; set; }
    public QuestionType Type { get; set; }
    public DifficultyLevel Difficulty { get; set; }

    public string TextSnapshot { get; set; } = string.Empty;
    public string? ImageUrlSnapshot { get; set; }
    public string? CorrectAnswerTextSnapshot { get; set; }

    public ICollection<AttemptQuestionOption> Options { get; set; } = new List<AttemptQuestionOption>();
}
```

Create `src/ExamSystem.Domain/Attempts/AttemptQuestionOption.cs`:

```csharp
namespace ExamSystem.Domain.Attempts;

public class AttemptQuestionOption : BaseEntity
{
    public Guid AttemptQuestionId { get; set; }
    public string TextSnapshot { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~EntityDefaultsTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ExamSystem.Domain/Candidates/ src/ExamSystem.Domain/Attempts/ tests/ExamSystem.Application.UnitTests/Domain/EntityDefaultsTests.cs
git commit -m "feat(domain): add Candidate, ExamAttempt, and attempt snapshot entities"
```

---

## Task 3: Persistence — DbSets, EF configurations, migration

**Files:**
- Modify: `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/CandidateExamAttemptGrantConfiguration.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/ExamAttemptConfiguration.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/AttemptQuestionConfiguration.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/AttemptQuestionOptionConfiguration.cs`

- [ ] **Step 1: Add DbSets to the context interface**

Modify `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs` — add these usings and members:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
```

Add inside the interface body (after the existing DbSets):

```csharp
    DbSet<Candidate> Candidates { get; }
    DbSet<CandidateExamAttemptGrant> CandidateExamAttemptGrants { get; }
    DbSet<ExamAttempt> ExamAttempts { get; }
    DbSet<AttemptQuestion> AttemptQuestions { get; }
    DbSet<AttemptQuestionOption> AttemptQuestionOptions { get; }
```

- [ ] **Step 2: Add DbSets to `ApplicationDbContext`**

Modify `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs` — add usings:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
```

Add after the existing `DbSet` properties:

```csharp
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateExamAttemptGrant> CandidateExamAttemptGrants => Set<CandidateExamAttemptGrant>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<AttemptQuestion> AttemptQuestions => Set<AttemptQuestion>();
    public DbSet<AttemptQuestionOption> AttemptQuestionOptions => Set<AttemptQuestionOption>();
```

- [ ] **Step 3: Write the EF configurations**

Create `src/ExamSystem.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs`:

```csharp
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
```

Create `src/ExamSystem.Infrastructure/Persistence/Configurations/CandidateExamAttemptGrantConfiguration.cs`:

```csharp
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
```

Create `src/ExamSystem.Infrastructure/Persistence/Configurations/ExamAttemptConfiguration.cs`:

```csharp
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
```

Create `src/ExamSystem.Infrastructure/Persistence/Configurations/AttemptQuestionConfiguration.cs`:

```csharp
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
```

Create `src/ExamSystem.Infrastructure/Persistence/Configurations/AttemptQuestionOptionConfiguration.cs`:

```csharp
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
```

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build ExamSystem.sln`
Expected: Build succeeded (the interface and its implementation now match; configs are auto-applied by `ApplyConfigurationsFromAssembly`).

- [ ] **Step 5: Generate the migration**

Run:
```bash
dotnet ef migrations add AddCandidatesAndAttempts \
  --project src/ExamSystem.Infrastructure \
  --startup-project src/ExamSystem.Api \
  --output-dir Migrations
```
Expected: a new `Migrations/*_AddCandidatesAndAttempts.cs` is created. Open it and confirm it creates `Candidates`, `CandidateExamAttemptGrants`, `ExamAttempts`, `AttemptQuestions`, `AttemptQuestionOptions` and the unique index on `Candidates.NationalId`.

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs src/ExamSystem.Infrastructure/Persistence/
git commit -m "feat(infra): persist candidate and attempt entities + migration"
```

---

## Task 4: `IQuestionSelectionService` (deterministic seeded selection)

**Files:**
- Create: `src/ExamSystem.Application/Common/Interfaces/IQuestionSelectionService.cs`
- Create: `src/ExamSystem.Infrastructure/Selection/QuestionSelectionService.cs`
- Modify: `src/ExamSystem.Infrastructure/DependencyInjection.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Selection/QuestionSelectionServiceTests.cs`

- [ ] **Step 1: Define the interface**

Create `src/ExamSystem.Application/Common/Interfaces/IQuestionSelectionService.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Builds the ordered, immutable question snapshot for one attempt (FR-2.2/2.3/2.5):
/// per topic (by DisplayOrder), per difficulty (Easy→Medium→Hard), a seeded random sample.
/// Returns a failure if any pool has fewer active questions than required.
/// </summary>
public interface IQuestionSelectionService
{
    Task<Result<List<AttemptQuestion>>> BuildSnapshotAsync(Exam exam, int seed, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Write the failing test**

Create `tests/ExamSystem.Application.UnitTests/Selection/QuestionSelectionServiceTests.cs`:

```csharp
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Selection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Selection;

public class QuestionSelectionServiceTests
{
    private static Question Mcq(Guid topicId, string text) => new()
    {
        TopicId = topicId, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
        Text = text, IsActive = true,
        Options = new List<QuestionOption>
        {
            new() { Text = "A", IsCorrect = false, DisplayOrder = 1 },
            new() { Text = "B", IsCorrect = true, DisplayOrder = 2 }
        }
    };

    [Fact]
    public async Task BuildSnapshot_SelectsRequestedCount_DeterministicallyBySeed()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        for (var i = 0; i < 5; i++) db.Questions.Add(Mcq(topic.Id, $"Q{i}"));
        var exam = new Exam { Name = "E", DurationMinutes = 30 };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 3 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new QuestionSelectionService(db);
        var full = await db.Exams.Include(e => e.TopicSelections).SingleAsync();

        var a = await service.BuildSnapshotAsync(full, seed: 42, CancellationToken.None);
        var b = await service.BuildSnapshotAsync(full, seed: 42, CancellationToken.None);

        Assert.True(a.IsSuccess);
        Assert.Equal(3, a.Value!.Count);
        Assert.All(a.Value, q => Assert.Equal(2, q.Options.Count));
        Assert.Equal(1, a.Value[0].DisplayOrder);
        Assert.Equal(
            a.Value.Select(q => q.SourceQuestionId),
            b.Value!.Select(q => q.SourceQuestionId)); // same seed -> same selection
    }

    [Fact]
    public async Task BuildSnapshot_InsufficientPool_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        db.Questions.Add(Mcq(topic.Id, "only one"));
        var exam = new Exam { Name = "E", DurationMinutes = 30 };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 3 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var service = new QuestionSelectionService(db);
        var full = await db.Exams.Include(e => e.TopicSelections).SingleAsync();

        var result = await service.BuildSnapshotAsync(full, seed: 1, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~QuestionSelectionServiceTests`
Expected: FAIL — `QuestionSelectionService` does not exist.

- [ ] **Step 4: Implement the service**

Create `src/ExamSystem.Infrastructure/Selection/QuestionSelectionService.cs`:

```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Common.Models;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Infrastructure.Selection;

public class QuestionSelectionService : IQuestionSelectionService
{
    private static readonly DifficultyLevel[] DifficultyOrder =
        { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard };

    private readonly IApplicationDbContext _db;

    public QuestionSelectionService(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<AttemptQuestion>>> BuildSnapshotAsync(
        Exam exam, int seed, CancellationToken cancellationToken)
    {
        var random = new Random(seed);
        var snapshot = new List<AttemptQuestion>();
        var order = 1;

        foreach (var selection in exam.TopicSelections
                     .OrderBy(s => s.DisplayOrder)
                     .ThenBy(s => Array.IndexOf(DifficultyOrder, s.Difficulty)))
        {
            var pool = await _db.Questions
                .Include(q => q.Options)
                .Where(q => q.TopicId == selection.TopicId
                            && q.Type == selection.Type
                            && q.Difficulty == selection.Difficulty
                            && q.IsActive)
                .ToListAsync(cancellationToken);

            if (pool.Count < selection.Count)
            {
                return Result<List<AttemptQuestion>>.Failure(
                    $"Insufficient questions for topic {selection.TopicId} ({selection.Difficulty}/{selection.Type}): " +
                    $"need {selection.Count}, have {pool.Count}.");
            }

            foreach (var question in pool.OrderBy(_ => random.Next()).Take(selection.Count))
            {
                snapshot.Add(new AttemptQuestion
                {
                    SourceQuestionId = question.Id,
                    TopicId = question.TopicId,
                    DisplayOrder = order++,
                    Type = question.Type,
                    Difficulty = question.Difficulty,
                    TextSnapshot = question.Text,
                    ImageUrlSnapshot = question.ImageUrl,
                    CorrectAnswerTextSnapshot = question.CorrectAnswerText,
                    Options = question.Options
                        .OrderBy(o => o.DisplayOrder)
                        .Select(o => new AttemptQuestionOption
                        {
                            TextSnapshot = o.Text,
                            IsCorrect = o.IsCorrect,
                            DisplayOrder = o.DisplayOrder
                        })
                        .ToList()
                });
            }
        }

        return Result<List<AttemptQuestion>>.Success(snapshot);
    }
}
```

- [ ] **Step 5: Register the service**

Modify `src/ExamSystem.Infrastructure/DependencyInjection.cs` — add usings at top:

```csharp
using ExamSystem.Infrastructure.Selection;
```

Add before `return services;`:

```csharp
        services.AddScoped<IQuestionSelectionService, QuestionSelectionService>();
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~QuestionSelectionServiceTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application/Common/Interfaces/IQuestionSelectionService.cs src/ExamSystem.Infrastructure/Selection/ src/ExamSystem.Infrastructure/DependencyInjection.cs tests/ExamSystem.Application.UnitTests/Selection/
git commit -m "feat: deterministic seeded question-selection service for attempt snapshots"
```

---

## Task 5: `GetExamLandingQuery`

**Files:**
- Create: `src/ExamSystem.Application/Features/CandidateExam/GetExamLanding/ExamLandingDto.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/GetExamLanding/GetExamLandingQuery.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/GetExamLanding/GetExamLandingQueryHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetExamLandingQueryHandlerTests.cs`

- [ ] **Step 1: Write the DTO and query**

Create `src/ExamSystem.Application/Features/CandidateExam/GetExamLanding/ExamLandingDto.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.GetExamLanding;

public record ExamLandingDto(
    Guid ExamId,
    string Name,
    string? Description,
    bool IsOpen,
    int DurationMinutes,
    int TotalQuestionCount);
```

Create `src/ExamSystem.Application/Features/CandidateExam/GetExamLanding/GetExamLandingQuery.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.GetExamLanding;

namespace ExamSystem.Application.Features.CandidateExam.GetExamLanding;

public record GetExamLandingQuery(Guid ExamId) : IRequest<Result<ExamLandingDto>>;
```

- [ ] **Step 2: Write the failing test**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetExamLandingQueryHandlerTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.GetExamLanding;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class GetExamLandingQueryHandlerTests
{
    [Fact]
    public async Task Handle_PublishedExamWithinWindow_ReturnsOpenWithQuestionCount()
    {
        using var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        var exam = new Exam
        {
            Name = "Skills", DurationMinutes = 90, Status = ExamStatus.Published,
            StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
        };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 25 });
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Hard, Type = QuestionType.FillBlank, Count = 5 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLandingQueryHandler(db);
        var result = await handler.Handle(new GetExamLandingQuery(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsOpen);
        Assert.Equal(30, result.Value.TotalQuestionCount);
        Assert.Equal(90, result.Value.DurationMinutes);
    }

    [Fact]
    public async Task Handle_DraftExam_IsNotOpen()
    {
        using var db = TestDbContextFactory.Create();
        var exam = new Exam
        {
            Name = "Draft", DurationMinutes = 60, Status = ExamStatus.Draft,
            StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
        };
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLandingQueryHandler(db);
        var result = await handler.Handle(new GetExamLandingQuery(exam.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsOpen);
    }

    [Fact]
    public async Task Handle_MissingExam_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new GetExamLandingQueryHandler(db);

        var result = await handler.Handle(new GetExamLandingQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~GetExamLandingQueryHandlerTests`
Expected: FAIL — handler does not exist.

- [ ] **Step 4: Implement the handler**

Create `src/ExamSystem.Application/Features/CandidateExam/GetExamLanding/GetExamLandingQueryHandler.cs`:

```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.CandidateExam.GetExamLanding;

public class GetExamLandingQueryHandler : IRequestHandler<GetExamLandingQuery, Result<ExamLandingDto>>
{
    private readonly IApplicationDbContext _db;

    public GetExamLandingQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<ExamLandingDto>> Handle(GetExamLandingQuery request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);

        if (exam is null)
        {
            return Result<ExamLandingDto>.Failure("Exam not found.");
        }

        var now = DateTime.UtcNow;
        var isOpen = exam.Status == ExamStatus.Published && now >= exam.StartAtUtc && now <= exam.EndAtUtc;
        var totalQuestions = exam.TopicSelections.Sum(s => s.Count);

        return Result<ExamLandingDto>.Success(new ExamLandingDto(
            exam.Id, exam.Name, exam.Description, isOpen, exam.DurationMinutes, totalQuestions));
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~GetExamLandingQueryHandlerTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/GetExamLanding/ tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetExamLandingQueryHandlerTests.cs
git commit -m "feat(app): GetExamLanding query for candidate entry"
```

---

## Task 6: `RegisterCandidateCommand` (+ validator)

**Files:**
- Create: `src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/RegisterCandidateCommand.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/RegisterResultDto.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/RegisterCandidateCommandValidator.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/RegisterCandidateCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RegisterCandidateCommandHandlerTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RegisterCandidateCommandValidatorTests.cs`

- [ ] **Step 1: Write the command + DTO**

Create `src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/RegisterResultDto.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.RegisterCandidate;

public enum RegisterOutcome { CanStart, AlreadyTaken, NotOpen }

public record RegisterResultDto(RegisterOutcome Status, Guid CandidateId);
```

Create `src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/RegisterCandidateCommand.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.RegisterCandidate;

public record RegisterCandidateCommand(
    Guid ExamId,
    string FullName,
    string NationalId,
    string MobileNumber) : IRequest<Result<RegisterResultDto>>;
```

- [ ] **Step 2: Write the validator**

Create `src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/RegisterCandidateCommandValidator.cs`:

```csharp
using System.Text.RegularExpressions;
using ExamSystem.Domain.Candidates;

namespace ExamSystem.Application.Features.CandidateExam.RegisterCandidate;

public class RegisterCandidateCommandValidator : AbstractValidator<RegisterCandidateCommand>
{
    public RegisterCandidateCommandValidator()
    {
        RuleFor(x => x.FullName)
            .Must(name => !string.IsNullOrWhiteSpace(name)
                          && name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4)
            .WithMessage("Full name must contain at least four words.");

        RuleFor(x => x.NationalId)
            .Must(value => NationalId.TryParse(value, out _, out _))
            .WithMessage("National ID is invalid.");

        RuleFor(x => x.MobileNumber)
            .Matches(new Regex(@"^01[0125]\d{8}$"))
            .WithMessage("Mobile number must be 11 digits starting with 010, 011, 012, or 015.");
    }
}
```

- [ ] **Step 3: Write the failing handler tests**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RegisterCandidateCommandHandlerTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.RegisterCandidate;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class RegisterCandidateCommandHandlerTests
{
    private const string Nid = "29912310123454";

    private static Exam OpenExam() => new()
    {
        Name = "E", DurationMinutes = 60, Status = ExamStatus.Published,
        StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
    };

    [Fact]
    public async Task Handle_NewCandidate_CreatesProfileAndReturnsCanStart()
    {
        using var db = TestDbContextFactory.Create();
        var exam = OpenExam();
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCandidateCommandHandler(db);
        var result = await handler.Handle(
            new RegisterCandidateCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterOutcome.CanStart, result.Value!.Status);
        var candidate = Assert.Single(db.Candidates);
        Assert.Equal(Nid, candidate.NationalId);
        Assert.Equal(Gender.Female, candidate.Gender);
    }

    [Fact]
    public async Task Handle_ClosedExam_ReturnsNotOpen()
    {
        using var db = TestDbContextFactory.Create();
        var exam = OpenExam();
        exam.Status = ExamStatus.Draft;
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCandidateCommandHandler(db);
        var result = await handler.Handle(
            new RegisterCandidateCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterOutcome.NotOpen, result.Value!.Status);
    }

    [Fact]
    public async Task Handle_AlreadyAttemptedNoGrant_ReturnsAlreadyTaken()
    {
        using var db = TestDbContextFactory.Create();
        var exam = OpenExam();
        var candidate = new Candidate { NationalId = Nid, FullName = "x", MobileNumber = "01012345678" };
        db.Exams.Add(exam);
        db.Candidates.Add(candidate);
        db.ExamAttempts.Add(new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = candidate.Id,
            StartedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(60)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCandidateCommandHandler(db);
        var result = await handler.Handle(
            new RegisterCandidateCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterOutcome.AlreadyTaken, result.Value!.Status);
    }

    [Fact]
    public async Task Handle_AlreadyAttemptedWithActiveGrant_ReturnsCanStart()
    {
        using var db = TestDbContextFactory.Create();
        var exam = OpenExam();
        var candidate = new Candidate { NationalId = Nid, FullName = "x", MobileNumber = "01012345678" };
        db.Exams.Add(exam);
        db.Candidates.Add(candidate);
        db.ExamAttempts.Add(new ExamAttempt
        {
            ExamId = exam.Id, CandidateId = candidate.Id,
            StartedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(60),
            Status = ExamAttemptStatus.Submitted
        });
        db.CandidateExamAttemptGrants.Add(new CandidateExamAttemptGrant
        { CandidateId = candidate.Id, ExamId = exam.Id, IsActive = true });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RegisterCandidateCommandHandler(db);
        var result = await handler.Handle(
            new RegisterCandidateCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegisterOutcome.CanStart, result.Value!.Status);
    }
}
```

- [ ] **Step 4: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~RegisterCandidateCommandHandlerTests`
Expected: FAIL — handler does not exist.

- [ ] **Step 5: Implement the handler**

Create `src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/RegisterCandidateCommandHandler.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.CandidateExam.RegisterCandidate;

public class RegisterCandidateCommandHandler : IRequestHandler<RegisterCandidateCommand, Result<RegisterResultDto>>
{
    private readonly IApplicationDbContext _db;

    public RegisterCandidateCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<RegisterResultDto>> Handle(RegisterCandidateCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<RegisterResultDto>.Failure("Exam not found.");
        }

        // Structural validity is guaranteed by the validator; parse again to derive fields.
        NationalId.TryParse(request.NationalId, out var parsed, out _);

        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.NationalId == request.NationalId, cancellationToken);

        if (candidate is null)
        {
            candidate = new Candidate
            {
                NationalId = request.NationalId,
                FullName = request.FullName.Trim(),
                MobileNumber = request.MobileNumber,
                BirthDateUtc = parsed!.BirthDateUtc,
                Gender = parsed.Gender,
                GovernorateCode = parsed.GovernorateCode
            };
            _db.Candidates.Add(candidate);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        var isOpen = exam.Status == ExamStatus.Published && now >= exam.StartAtUtc && now <= exam.EndAtUtc;
        if (!isOpen)
        {
            return Result<RegisterResultDto>.Success(new RegisterResultDto(RegisterOutcome.NotOpen, candidate.Id));
        }

        var hasAttempt = await _db.ExamAttempts
            .AnyAsync(a => a.ExamId == exam.Id && a.CandidateId == candidate.Id, cancellationToken);
        var hasActiveGrant = await _db.CandidateExamAttemptGrants
            .AnyAsync(g => g.ExamId == exam.Id && g.CandidateId == candidate.Id && g.IsActive, cancellationToken);

        var outcome = hasAttempt && !hasActiveGrant ? RegisterOutcome.AlreadyTaken : RegisterOutcome.CanStart;
        return Result<RegisterResultDto>.Success(new RegisterResultDto(outcome, candidate.Id));
    }
}
```

- [ ] **Step 6: Write validator tests**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RegisterCandidateCommandValidatorTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.RegisterCandidate;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class RegisterCandidateCommandValidatorTests
{
    private readonly RegisterCandidateCommandValidator _validator = new();

    private static RegisterCandidateCommand Cmd(string name, string nid, string mobile) =>
        new(System.Guid.NewGuid(), name, nid, mobile);

    [Fact]
    public void Valid_Passes()
    {
        var result = _validator.Validate(Cmd("احمد محمد علي حسن", "29912310123454", "01012345678"));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("احمد محمد", "29912310123454", "01012345678")]   // 2 words
    [InlineData("احمد محمد علي حسن", "123", "01012345678")]        // bad NID
    [InlineData("احمد محمد علي حسن", "29912310123454", "01312345678")] // bad mobile prefix
    public void Invalid_Fails(string name, string nid, string mobile)
    {
        var result = _validator.Validate(Cmd(name, nid, mobile));
        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 7: Run to verify everything passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~RegisterCandidate`
Expected: PASS (handler + validator tests).

- [ ] **Step 8: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/RegisterCandidate/ tests/ExamSystem.Application.UnitTests/Features/CandidateExam/RegisterCandidate*
git commit -m "feat(app): candidate registration with profile reuse and already-taken guard"
```

---

## Task 7: Attempt token generator + `AttemptToken` auth scheme

**Files:**
- Create: `src/ExamSystem.Application/Common/Interfaces/IAttemptTokenGenerator.cs`
- Create: `src/ExamSystem.Infrastructure/Identity/AttemptTokenSettings.cs`
- Create: `src/ExamSystem.Infrastructure/Identity/AttemptTokenGenerator.cs`
- Modify: `src/ExamSystem.Infrastructure/DependencyInjection.cs`
- Modify: `src/ExamSystem.Api/Program.cs`
- Modify: `src/ExamSystem.Api/appsettings.json`

- [ ] **Step 1: Define the interface**

Create `src/ExamSystem.Application/Common/Interfaces/IAttemptTokenGenerator.cs`:

```csharp
namespace ExamSystem.Application.Common.Interfaces;

public interface IAttemptTokenGenerator
{
    string GenerateToken(Guid attemptId, Guid candidateId, Guid examId, DateTime expiresAtUtc);
}
```

- [ ] **Step 2: Implement settings + generator**

Create `src/ExamSystem.Infrastructure/Identity/AttemptTokenSettings.cs`:

```csharp
namespace ExamSystem.Infrastructure.Identity;

public class AttemptTokenSettings
{
    public const string SectionName = "AttemptToken";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ExamSystem";
    public string Audience { get; set; } = "ExamSystemCandidates";
}
```

Create `src/ExamSystem.Infrastructure/Identity/AttemptTokenGenerator.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExamSystem.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ExamSystem.Infrastructure.Identity;

/// <summary>Issues a short-lived candidate attempt token (distinct from the admin JWT).</summary>
public class AttemptTokenGenerator : IAttemptTokenGenerator
{
    public const string AttemptIdClaim = "attempt_id";
    public const string CandidateIdClaim = "candidate_id";
    public const string ExamIdClaim = "exam_id";

    private readonly AttemptTokenSettings _settings;

    public AttemptTokenGenerator(IOptions<AttemptTokenSettings> options)
    {
        _settings = options.Value;
        if (string.IsNullOrWhiteSpace(_settings.Key) || Encoding.UTF8.GetByteCount(_settings.Key) < 32)
        {
            throw new InvalidOperationException("AttemptToken:Key must be configured and at least 32 bytes.");
        }
    }

    public string GenerateToken(Guid attemptId, Guid candidateId, Guid examId, DateTime expiresAtUtc)
    {
        var claims = new[]
        {
            new Claim(AttemptIdClaim, attemptId.ToString()),
            new Claim(CandidateIdClaim, candidateId.ToString()),
            new Claim(ExamIdClaim, examId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer, audience: _settings.Audience, claims: claims,
            expires: expiresAtUtc, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 3: Register generator + settings**

Modify `src/ExamSystem.Infrastructure/DependencyInjection.cs` — add before `return services;`:

```csharp
        services.Configure<AttemptTokenSettings>(configuration.GetSection(AttemptTokenSettings.SectionName));
        services.AddScoped<IAttemptTokenGenerator, AttemptTokenGenerator>();
```

(`ExamSystem.Infrastructure.Identity` is already imported in this file.)

- [ ] **Step 4: Register the `AttemptToken` JWT scheme**

Modify `src/ExamSystem.Api/Program.cs`. After the existing `AddJwtBearer()` registration for the admin scheme, extend the authentication builder with a second named scheme:

```csharp
using ExamSystem.Infrastructure.Identity; // add with the other usings at the top

// ... within the AddAuthentication(...) chain, after .AddJwtBearer():
    .AddJwtBearer("AttemptToken", options =>
    {
        var settings = builder.Configuration.GetSection(AttemptTokenSettings.SectionName).Get<AttemptTokenSettings>()
                       ?? new AttemptTokenSettings();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                string.IsNullOrWhiteSpace(settings.Key) ? new string('0', 32) : settings.Key))
        };
    });
```

> The scheme is issued in Slice 1a and enforced by Slice 1b endpoints via
> `[Authorize(AuthenticationSchemes = "AttemptToken")]`. No 1a endpoint enforces it yet.

- [ ] **Step 5: Add a default config key**

Modify `src/ExamSystem.Api/appsettings.json` — add a sibling to the existing `Jwt` block:

```json
  "AttemptToken": {
    "Issuer": "ExamSystem",
    "Audience": "ExamSystemCandidates"
  },
```

Then set the real key locally (not committed):

```bash
cd src/ExamSystem.Api
dotnet user-secrets set "AttemptToken:Key" "$(openssl rand -base64 32)"
```

- [ ] **Step 6: Verify build**

Run: `dotnet build ExamSystem.sln`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application/Common/Interfaces/IAttemptTokenGenerator.cs src/ExamSystem.Infrastructure/Identity/AttemptToken*.cs src/ExamSystem.Infrastructure/DependencyInjection.cs src/ExamSystem.Api/Program.cs src/ExamSystem.Api/appsettings.json
git commit -m "feat: candidate attempt-token generator and AttemptToken auth scheme"
```

---

## Task 8: `StartAttemptCommand` (create attempt + snapshot + token)

**Files:**
- Create: `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptDto.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommand.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptCommandHandlerTests.cs`

- [ ] **Step 1: Write the command + DTO**

Create `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptDto.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

public record StartAttemptDto(Guid AttemptId, string AttemptToken, DateTime ExpiresAtUtc);
```

Create `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommand.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

public record StartAttemptCommand(
    Guid ExamId,
    string FullName,
    string NationalId,
    string MobileNumber) : IRequest<Result<StartAttemptDto>>;
```

- [ ] **Step 2: Write the failing test**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptCommandHandlerTests.cs`:

```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Features.CandidateExam.StartAttempt;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Selection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class StartAttemptCommandHandlerTests
{
    private const string Nid = "29912310123454";

    private sealed class FakeTokenGenerator : IAttemptTokenGenerator
    {
        public string GenerateToken(Guid attemptId, Guid candidateId, Guid examId, DateTime expiresAtUtc)
            => $"token-{attemptId}";
    }

    private static Question Mcq(Guid topicId) => new()
    {
        TopicId = topicId, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium, Text = "Q", IsActive = true,
        Options = new List<QuestionOption> { new() { Text = "A", IsCorrect = true, DisplayOrder = 1 } }
    };

    private static async Task<(ApplicationDbContextStub db, Exam exam)> SeedAsync()
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        for (var i = 0; i < 3; i++) db.Questions.Add(Mcq(topic.Id));
        var exam = new Exam
        {
            Name = "E", DurationMinutes = 60, Status = ExamStatus.Published,
            StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
        };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 2 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, exam);
    }

    [Fact]
    public async Task Handle_NewCandidate_CreatesAttemptWithSnapshotTimerAndToken()
    {
        var (db, exam) = await SeedAsync();
        var handler = new StartAttemptCommandHandler(db, new QuestionSelectionService(db), new FakeTokenGenerator());

        var result = await handler.Handle(
            new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attempt = db.ExamAttempts.Include(a => a.Questions).Single();
        Assert.Equal(ExamAttemptStatus.InProgress, attempt.Status);
        Assert.Equal(2, attempt.Questions.Count);
        Assert.Equal(attempt.StartedAtUtc.AddMinutes(60), attempt.ExpiresAtUtc);
        Assert.Equal($"token-{attempt.Id}", result.Value!.AttemptToken);
    }

    [Fact]
    public async Task Handle_ExistingInProgressAttempt_IsIdempotent()
    {
        var (db, exam) = await SeedAsync();
        var handler = new StartAttemptCommandHandler(db, new QuestionSelectionService(db), new FakeTokenGenerator());
        var cmd = new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678");

        var first = await handler.Handle(cmd, CancellationToken.None);
        var second = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(first.Value!.AttemptId, second.Value!.AttemptId);
        Assert.Single(db.ExamAttempts);
    }

    [Fact]
    public async Task Handle_AlreadyTakenNoGrant_Fails()
    {
        var (db, exam) = await SeedAsync();
        var handler = new StartAttemptCommandHandler(db, new QuestionSelectionService(db), new FakeTokenGenerator());
        var cmd = new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678");

        var first = await handler.Handle(cmd, CancellationToken.None);
        // mark it submitted so it is no longer "in progress"
        var attempt = db.ExamAttempts.Single();
        attempt.Status = ExamAttemptStatus.Submitted;
        await db.SaveChangesAsync(CancellationToken.None);

        var second = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
    }
}
```

> Note: `ApplicationDbContextStub` in the signature above is just the concrete `ApplicationDbContext`
> returned by `TestDbContextFactory.Create()`; replace the tuple type with `(ApplicationDbContext db, Exam exam)`.
> (Use `ExamSystem.Infrastructure.Persistence.ApplicationDbContext` — add the using.)

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~StartAttemptCommandHandlerTests`
Expected: FAIL — handler does not exist.

- [ ] **Step 4: Implement the handler**

Create `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommandHandler.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

public class StartAttemptCommandHandler : IRequestHandler<StartAttemptCommand, Result<StartAttemptDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQuestionSelectionService _selection;
    private readonly IAttemptTokenGenerator _tokens;

    public StartAttemptCommandHandler(
        IApplicationDbContext db, IQuestionSelectionService selection, IAttemptTokenGenerator tokens)
    {
        _db = db;
        _selection = selection;
        _tokens = tokens;
    }

    public async Task<Result<StartAttemptDto>> Handle(StartAttemptCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .Include(e => e.TopicSelections)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<StartAttemptDto>.Failure("Exam not found.");
        }

        var now = DateTime.UtcNow;
        if (!(exam.Status == ExamStatus.Published && now >= exam.StartAtUtc && now <= exam.EndAtUtc))
        {
            return Result<StartAttemptDto>.Failure("Exam is not open.");
        }

        NationalId.TryParse(request.NationalId, out var parsed, out _);
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.NationalId == request.NationalId, cancellationToken);
        if (candidate is null)
        {
            candidate = new Candidate
            {
                NationalId = request.NationalId,
                FullName = request.FullName.Trim(),
                MobileNumber = request.MobileNumber,
                BirthDateUtc = parsed!.BirthDateUtc,
                Gender = parsed.Gender,
                GovernorateCode = parsed.GovernorateCode
            };
            _db.Candidates.Add(candidate);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Resume: an in-progress attempt for this (candidate, exam) is returned as-is.
        var existing = await _db.ExamAttempts
            .FirstOrDefaultAsync(a => a.ExamId == exam.Id && a.CandidateId == candidate.Id
                                      && a.Status == ExamAttemptStatus.InProgress, cancellationToken);
        if (existing is not null)
        {
            return Ok(existing, candidate.Id, exam.Id);
        }

        // Otherwise: any prior attempt (without an active grant) blocks a new one (FR-1.5.1).
        var hasAnyAttempt = await _db.ExamAttempts
            .AnyAsync(a => a.ExamId == exam.Id && a.CandidateId == candidate.Id, cancellationToken);
        var hasActiveGrant = await _db.CandidateExamAttemptGrants
            .AnyAsync(g => g.ExamId == exam.Id && g.CandidateId == candidate.Id && g.IsActive, cancellationToken);
        if (hasAnyAttempt && !hasActiveGrant)
        {
            return Result<StartAttemptDto>.Failure("You have already taken this exam.");
        }

        var attempt = new ExamAttempt
        {
            ExamId = exam.Id,
            CandidateId = candidate.Id,
            StartedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(exam.DurationMinutes),
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Seed = attempt.Id.GetHashCode();

        var snapshot = await _selection.BuildSnapshotAsync(exam, attempt.Seed, cancellationToken);
        if (!snapshot.IsSuccess)
        {
            return Result<StartAttemptDto>.Failure(snapshot.Errors);
        }
        foreach (var q in snapshot.Value!)
        {
            attempt.Questions.Add(q);
        }

        _db.ExamAttempts.Add(attempt);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(attempt, candidate.Id, exam.Id);
    }

    private Result<StartAttemptDto> Ok(ExamAttempt attempt, Guid candidateId, Guid examId)
    {
        var token = _tokens.GenerateToken(attempt.Id, candidateId, examId, attempt.ExpiresAtUtc);
        return Result<StartAttemptDto>.Success(new StartAttemptDto(attempt.Id, token, attempt.ExpiresAtUtc));
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~StartAttemptCommandHandlerTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/StartAttempt/ tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptCommandHandlerTests.cs
git commit -m "feat(app): StartAttempt creates attempt, snapshot, server timer, and token"
```

---

## Task 9: `CandidateExamController` + integration tests

**Files:**
- Create: `src/ExamSystem.Api/Controllers/CandidateExamController.cs`
- Test: `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateExamControllerTests.cs`

- [ ] **Step 1: Write the controller**

Create `src/ExamSystem.Api/Controllers/CandidateExamController.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.GetExamLanding;
using ExamSystem.Application.Features.CandidateExam.RegisterCandidate;
using ExamSystem.Application.Features.CandidateExam.StartAttempt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Public candidate-facing exam entry (Slice 1a): landing, registration, start.</summary>
[ApiController]
[Route("api/exam")]
[AllowAnonymous]
public class CandidateExamController : ControllerBase
{
    private readonly ISender _sender;

    public CandidateExamController(ISender sender) => _sender = sender;

    [HttpGet("{examId:guid}/landing")]
    public async Task<IActionResult> Landing(Guid examId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamLandingQuery(examId), cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    [HttpPost("{examId:guid}/register")]
    public async Task<IActionResult> Register(Guid examId, [FromBody] CandidateIdentityRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCandidateCommand(examId, request.FullName, request.NationalId, request.MobileNumber);
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(new { status = result.Value!.Status.ToString(), candidateId = result.Value.CandidateId });
    }

    [HttpPost("{examId:guid}/start")]
    public async Task<IActionResult> Start(Guid examId, [FromBody] CandidateIdentityRequest request, CancellationToken cancellationToken)
    {
        var command = new StartAttemptCommand(examId, request.FullName, request.NationalId, request.MobileNumber);
        var result = await _sender.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    public record CandidateIdentityRequest(string FullName, string NationalId, string MobileNumber);
}
```

- [ ] **Step 2: Write failing integration tests**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateExamControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class CandidateExamControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CandidateExamControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    private const string Nid = "29912310123454";
    private static object Identity => new { fullName = "احمد محمد علي حسن", nationalId = Nid, mobileNumber = "01012345678" };

    // Builds a published, currently-open exam with a bank of 3 MCQs and a selection of 2.
    private static async Task<Guid> CreatePublishedExamAsync(HttpClient admin)
    {
        var topicResp = await admin.PostAsJsonAsync("/api/admin/topics", new { name = $"T{Guid.NewGuid():N}", displayOrder = 1 });
        topicResp.EnsureSuccessStatusCode();
        var topicId = (await topicResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        for (var i = 0; i < 3; i++)
        {
            var q = await admin.PostAsJsonAsync("/api/admin/questions", new
            {
                topicId, type = "Mcq", difficulty = "Medium", text = $"Q{i}",
                options = new[] { new { text = "A", isCorrect = false }, new { text = "B", isCorrect = true } }
            });
            q.EnsureSuccessStatusCode();
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

        var publishResp = await admin.PostAsync($"/api/admin/exams/{examId}/publish", null);
        publishResp.EnsureSuccessStatusCode();
        return examId;
    }

    [Fact]
    public async Task Landing_PublishedExam_ReturnsOpen()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);

        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/api/exam/{examId}/landing");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LandingResponse>();
        Assert.True(body!.IsOpen);
        Assert.Equal(2, body.TotalQuestionCount);
    }

    [Fact]
    public async Task Register_NewCandidate_ReturnsCanStart()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);

        var anon = _factory.CreateClient();
        var resp = await anon.PostAsJsonAsync($"/api/exam/{examId}/register", Identity);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.Equal("CanStart", body!.Status);
    }

    [Fact]
    public async Task Start_ThenStartAgain_IsIdempotentAndReturnsToken()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        var anon = _factory.CreateClient();

        var first = await anon.PostAsJsonAsync($"/api/exam/{examId}/start", Identity);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<StartResponse>();
        Assert.False(string.IsNullOrWhiteSpace(firstBody!.AttemptToken));

        var second = await anon.PostAsJsonAsync($"/api/exam/{examId}/start", Identity);
        var secondBody = await second.Content.ReadFromJsonAsync<StartResponse>();
        Assert.Equal(firstBody.AttemptId, secondBody!.AttemptId);
    }

    [Fact]
    public async Task Register_InvalidNationalId_ReturnsBadRequest()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreatePublishedExamAsync(admin);
        var anon = _factory.CreateClient();

        var resp = await anon.PostAsJsonAsync($"/api/exam/{examId}/register",
            new { fullName = "احمد محمد علي حسن", nationalId = "123", mobileNumber = "01012345678" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private record LandingResponse(Guid ExamId, string Name, string? Description, bool IsOpen, int DurationMinutes, int TotalQuestionCount);
    private record RegisterResponse(string Status, Guid CandidateId);
    private record StartResponse(Guid AttemptId, string AttemptToken, DateTime ExpiresAtUtc);
}
```

> `IdResponse` already exists in the integration-test project (used by the other controller tests). If the
> compiler reports it missing in this namespace, add `private record IdResponse(Guid Id);` to this class.

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter FullyQualifiedName~CandidateExamControllerTests`
Expected: FAIL — controller does not exist / route not found.

- [ ] **Step 4: Run to verify it passes (controller from Step 1 is already written)**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter FullyQualifiedName~CandidateExamControllerTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Run the full backend test suite**

Run: `dotnet test ExamSystem.sln`
Expected: All tests pass (existing admin tests + new candidate tests).

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Api/Controllers/CandidateExamController.cs tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateExamControllerTests.cs
git commit -m "feat(api): public candidate exam endpoints (landing, register, start)"
```

---

## Task 10: Candidate theme tokens + layout shell (frontend)

**Files:**
- Create: `frontend/src/styles/_candidate.scss`
- Modify: `frontend/src/styles.scss`
- Create: `frontend/src/app/features/candidate/candidate-layout.component.ts`

- [ ] **Step 1: Add candidate theme tokens + surface styles**

Create `frontend/src/styles/_candidate.scss`:

```scss
// Candidate surface — calm, focused, Ministry-of-Justice identity.
// Distinct from the admin "Official Ledger"; layered on the shared base tokens.
.candidate-surface {
  --judicial-green: #0f5c4a;      // primary action (complements the emblem's gold/laurel)
  --judicial-green-deep: #0b4438;
  --judicial-gold: #b8912f;       // identity accent ONLY (hairline / emblem frame), never text/buttons
  --candidate-bg: #f7f6f2;        // soft off-white canvas
  --candidate-ink: #1a1a1a;

  min-height: 100vh;
  display: flex;
  flex-direction: column;
  align-items: center;
  background: var(--candidate-bg);
  color: var(--candidate-ink);
  padding: var(--space-xl) var(--space-md);
}

.candidate-card {
  width: min(560px, 100%);
  background: var(--pure-white);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-resting);
  padding: var(--space-2xl) var(--space-xl);
  animation: ledger-rise-in var(--dur-slow) var(--ease-out);
}

.candidate-header {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-sm);
  text-align: center;
  padding-bottom: var(--space-lg);
  margin-bottom: var(--space-lg);
  border-bottom: 2px solid var(--judicial-gold);
}

.candidate-header img { width: 96px; height: 96px; object-fit: contain; }
.candidate-header .org { color: var(--slate-grey); font-size: var(--fs-body); line-height: 1.7; }
.candidate-header .exam-title { font-size: var(--fs-headline); font-weight: var(--fw-bold); }

.candidate-surface .btn-primary {
  width: 100%;
  background: var(--judicial-green);
  color: var(--pure-white);
  border: none;
  border-radius: var(--radius-md);
  padding: 14px 20px;
  font-size: var(--fs-title);
  font-weight: var(--fw-semibold);
  cursor: pointer;
  transition: background-color var(--dur-fast) var(--ease-out);
}
.candidate-surface .btn-primary:hover:not(:disabled) { background: var(--judicial-green-deep); }
.candidate-surface .btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }

.candidate-state {
  text-align: center;
  display: flex;
  flex-direction: column;
  gap: var(--space-md);
  padding: var(--space-lg) 0;
}
```

- [ ] **Step 2: Import the partial**

Modify `frontend/src/styles.scss` — add after the `@import "styles/pages";` line:

```scss
@import "styles/candidate";
```

- [ ] **Step 3: Create the layout shell**

Create `frontend/src/app/features/candidate/candidate-layout.component.ts`:

```typescript
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-candidate-layout',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="candidate-surface">
      <router-outlet />
    </div>
  `
})
export class CandidateLayoutComponent {}
```

- [ ] **Step 4: Commit**

```bash
git add frontend/src/styles/_candidate.scss frontend/src/styles.scss frontend/src/app/features/candidate/candidate-layout.component.ts
git commit -m "feat(candidate-ui): calm candidate theme tokens and layout shell"
```

---

## Task 11: Candidate service, token store, interceptor, routes

**Files:**
- Create: `frontend/src/app/core/services/candidate-exam.service.ts`
- Create: `frontend/src/app/core/services/attempt-token.store.ts`
- Create: `frontend/src/app/core/interceptors/attempt-token.interceptor.ts`
- Create: `frontend/src/app/features/candidate/candidate.routes.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.config.ts`
- Test: `frontend/src/app/core/services/candidate-exam.service.spec.ts`

- [ ] **Step 1: Create the attempt-token store**

Create `frontend/src/app/core/services/attempt-token.store.ts`:

```typescript
import { Injectable } from '@angular/core';

/** Stores the per-exam candidate attempt token separately from the admin auth token. */
@Injectable({ providedIn: 'root' })
export class AttemptTokenStore {
  private key(examId: string): string { return `attempt_${examId}`; }

  set(examId: string, token: string): void { localStorage.setItem(this.key(examId), token); }
  get(examId: string): string | null { return localStorage.getItem(this.key(examId)); }
  clear(examId: string): void { localStorage.removeItem(this.key(examId)); }
}
```

- [ ] **Step 2: Create the service**

Create `frontend/src/app/core/services/candidate-exam.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ExamLanding {
  examId: string;
  name: string;
  description: string | null;
  isOpen: boolean;
  durationMinutes: number;
  totalQuestionCount: number;
}

export type RegisterStatus = 'CanStart' | 'AlreadyTaken' | 'NotOpen';

export interface CandidateIdentity {
  fullName: string;
  nationalId: string;
  mobileNumber: string;
}

export interface RegisterResult { status: RegisterStatus; candidateId: string; }
export interface StartAttemptResult { attemptId: string; attemptToken: string; expiresAtUtc: string; }

@Injectable({ providedIn: 'root' })
export class CandidateExamService {
  private readonly baseUrl = `${environment.apiBaseUrl}/exam`;

  constructor(private readonly http: HttpClient) {}

  landing(examId: string): Observable<ExamLanding> {
    return this.http.get<ExamLanding>(`${this.baseUrl}/${examId}/landing`);
  }

  register(examId: string, identity: CandidateIdentity): Observable<RegisterResult> {
    return this.http.post<RegisterResult>(`${this.baseUrl}/${examId}/register`, identity);
  }

  start(examId: string, identity: CandidateIdentity): Observable<StartAttemptResult> {
    return this.http.post<StartAttemptResult>(`${this.baseUrl}/${examId}/start`, identity);
  }
}
```

- [ ] **Step 3: Write the service spec (failing until service compiles)**

Create `frontend/src/app/core/services/candidate-exam.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CandidateExamService } from './candidate-exam.service';
import { environment } from '../../../environments/environment';

describe('CandidateExamService', () => {
  let service: CandidateExamService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule], providers: [CandidateExamService] });
    service = TestBed.inject(CandidateExamService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts identity to the start endpoint and returns the attempt token', () => {
    const examId = 'abc';
    let result: string | undefined;
    service.start(examId, { fullName: 'a b c d', nationalId: '29912310123454', mobileNumber: '01012345678' })
      .subscribe(r => (result = r.attemptToken));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/exam/${examId}/start`);
    expect(req.request.method).toBe('POST');
    req.flush({ attemptId: 'id1', attemptToken: 'tok1', expiresAtUtc: '2026-07-10T10:00:00Z' });

    expect(result).toBe('tok1');
  });
});
```

- [ ] **Step 4: Create the interceptor**

Create `frontend/src/app/core/interceptors/attempt-token.interceptor.ts`:

```typescript
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AttemptTokenStore } from '../services/attempt-token.store';

/** Attaches the attempt token to candidate exam calls that target a specific exam id in the path. */
export const attemptTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const match = req.url.match(/\/api\/exam\/([0-9a-fA-F-]+)\//);
  if (!match) {
    return next(req);
  }
  const token = inject(AttemptTokenStore).get(match[1]);
  if (!token) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
```

- [ ] **Step 5: Register the interceptor**

Modify `frontend/src/app/app.config.ts` to include the new interceptor:

```typescript
import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { attemptTokenInterceptor } from './core/interceptors/attempt-token.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, attemptTokenInterceptor]))
  ]
};
```

- [ ] **Step 6: Create candidate routes and wire them into the app**

Create `frontend/src/app/features/candidate/candidate.routes.ts`:

```typescript
import { Routes } from '@angular/router';

export const candidateRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./candidate-layout.component').then(m => m.CandidateLayoutComponent),
    children: [
      {
        path: '',
        loadComponent: () => import('./exam-landing.component').then(m => m.ExamLandingComponent)
      },
      {
        path: 'instructions',
        loadComponent: () => import('./instructions.component').then(m => m.InstructionsComponent)
      },
      {
        path: 'attempt',
        loadComponent: () => import('./attempt-shell.component').then(m => m.AttemptShellComponent)
      }
    ]
  }
];
```

Modify `frontend/src/app/app.routes.ts` — add this route entry **before** the `admin` route:

```typescript
  {
    path: 'exam/:examId',
    loadChildren: () => import('./features/candidate/candidate.routes').then(m => m.candidateRoutes)
  },
```

- [ ] **Step 7: Run the spec (will fail to compile until Tasks 12-13 create the lazy components)**

> The route `loadComponent` imports reference components built in Tasks 12-13. Run the service spec in
> isolation now; run the full frontend build after Task 13.

Run: `cd frontend && npm test -- --include='**/candidate-exam.service.spec.ts' --watch=false`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/app/core/services/candidate-exam.service.ts frontend/src/app/core/services/candidate-exam.service.spec.ts frontend/src/app/core/services/attempt-token.store.ts frontend/src/app/core/interceptors/attempt-token.interceptor.ts frontend/src/app/features/candidate/candidate.routes.ts frontend/src/app/app.routes.ts frontend/src/app/app.config.ts
git commit -m "feat(candidate-ui): candidate service, attempt-token store/interceptor, routes"
```

---

## Task 12: `ExamLandingComponent` (branded header + registration form)

**Files:**
- Create: `frontend/src/app/features/candidate/exam-landing.component.ts`
- Create: `frontend/src/app/features/candidate/exam-landing.component.html`
- Test: `frontend/src/app/features/candidate/exam-landing.component.spec.ts`

- [ ] **Step 1: Write the component**

Create `frontend/src/app/features/candidate/exam-landing.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CandidateExamService, ExamLanding } from '../../core/services/candidate-exam.service';

const FOUR_WORDS = /^\s*\S+(\s+\S+){3,}\s*$/;
const NATIONAL_ID = /^[23]\d{13}$/;
const MOBILE = /^01[0125]\d{8}$/;

@Component({
  selector: 'app-exam-landing',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './exam-landing.component.html'
})
export class ExamLandingComponent implements OnInit {
  examId = '';
  landing: ExamLanding | null = null;
  loading = true;
  submitting = false;
  blockedMessage: string | null = null;

  readonly form = this.fb.group({
    fullName: ['', [Validators.required, Validators.pattern(FOUR_WORDS)]],
    nationalId: ['', [Validators.required, Validators.pattern(NATIONAL_ID)]],
    mobileNumber: ['', [Validators.required, Validators.pattern(MOBILE)]]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: CandidateExamService
  ) {}

  ngOnInit(): void {
    this.examId = this.route.parent?.snapshot.paramMap.get('examId') ?? '';
    this.service.landing(this.examId).subscribe({
      next: l => { this.landing = l; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting) { return; }
    this.submitting = true;
    this.blockedMessage = null;
    const identity = this.form.getRawValue() as { fullName: string; nationalId: string; mobileNumber: string };

    this.service.register(this.examId, identity).subscribe({
      next: res => {
        this.submitting = false;
        if (res.status === 'CanStart') {
          this.router.navigate(['instructions'], { relativeTo: this.route.parent, state: { identity } });
        } else if (res.status === 'AlreadyTaken') {
          this.blockedMessage = 'لقد أدّيت هذا الامتحان من قبل. لا يمكن إعادة الدخول إلا بتفعيل صريح من إدارة النظام.';
        } else {
          this.blockedMessage = 'هذا الامتحان غير متاح حالياً.';
        }
      },
      error: () => {
        this.submitting = false;
        this.blockedMessage = 'تعذّر التحقّق من البيانات — راجع الحقول وحاول مرة أخرى.';
      }
    });
  }
}
```

- [ ] **Step 2: Write the template**

Create `frontend/src/app/features/candidate/exam-landing.component.html`:

```html
<div class="candidate-card">
  <div class="candidate-header">
    <img src="assets/moj-logo.png" alt="شعار وزارة العدل" />
    <div class="org">وزارة العدل — قطاع التطوير التقني ومركز المعلومات القضائي</div>
    <div class="exam-title">{{ landing?.name || 'اختبار قياس المهارات الأساسية' }}</div>
  </div>

  <div class="candidate-state" *ngIf="loading">جارٍ التحميل…</div>

  <div class="candidate-state" *ngIf="!loading && landing && !landing.isOpen">
    هذا الامتحان غير متاح حالياً.
  </div>

  <div class="candidate-state" *ngIf="blockedMessage">{{ blockedMessage }}</div>

  <form *ngIf="!loading && landing?.isOpen && !blockedMessage" [formGroup]="form" (ngSubmit)="submit()">
    <div class="field">
      <label for="fullName">الاسم رباعياً</label>
      <input id="fullName" type="text" formControlName="fullName" autocomplete="name" />
      <p class="field-error" *ngIf="form.controls.fullName.touched && form.controls.fullName.invalid">
        ادخل الاسم رباعياً (٤ كلمات على الأقل).
      </p>
    </div>

    <div class="field">
      <label for="nationalId">الرقم القومي</label>
      <input id="nationalId" type="text" inputmode="numeric" maxlength="14" formControlName="nationalId" />
      <p class="field-error" *ngIf="form.controls.nationalId.touched && form.controls.nationalId.invalid">
        الرقم القومي يجب أن يكون ١٤ رقماً صحيح البنية.
      </p>
    </div>

    <div class="field">
      <label for="mobileNumber">رقم الموبايل</label>
      <input id="mobileNumber" type="text" inputmode="numeric" maxlength="11" formControlName="mobileNumber" />
      <p class="field-error" *ngIf="form.controls.mobileNumber.touched && form.controls.mobileNumber.invalid">
        رقم موبايل مصري صحيح (١١ رقماً يبدأ بـ 010/011/012/015).
      </p>
    </div>

    <button class="btn-primary" type="submit" [disabled]="form.invalid || submitting">
      {{ submitting ? 'جارٍ التحقّق…' : 'متابعة' }}
    </button>
  </form>
</div>
```

- [ ] **Step 3: Write the failing spec**

Create `frontend/src/app/features/candidate/exam-landing.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { ExamLandingComponent } from './exam-landing.component';
import { CandidateExamService } from '../../core/services/candidate-exam.service';

describe('ExamLandingComponent', () => {
  let fixture: ComponentFixture<ExamLandingComponent>;
  let component: ExamLandingComponent;
  const serviceStub = {
    landing: () => of({ examId: 'e1', name: 'Skills', description: null, isOpen: true, durationMinutes: 60, totalQuestionCount: 30 }),
    register: jasmine.createSpy('register').and.returnValue(of({ status: 'CanStart', candidateId: 'c1' }))
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExamLandingComponent, HttpClientTestingModule],
      providers: [
        { provide: CandidateExamService, useValue: serviceStub },
        { provide: ActivatedRoute, useValue: { parent: { snapshot: { paramMap: new Map([['examId', 'e1']]) } } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ExamLandingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('marks the form invalid when the national ID is malformed', () => {
    component.form.setValue({ fullName: 'a b c d', nationalId: '123', mobileNumber: '01012345678' });
    expect(component.form.invalid).toBeTrue();
  });

  it('accepts a well-formed identity', () => {
    component.form.setValue({ fullName: 'احمد محمد علي حسن', nationalId: '29912310123454', mobileNumber: '01012345678' });
    expect(component.form.valid).toBeTrue();
  });
});
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd frontend && npm test -- --include='**/exam-landing.component.spec.ts' --watch=false`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/candidate/exam-landing.component.*
git commit -m "feat(candidate-ui): branded exam landing page with validated registration form"
```

---

## Task 13: `InstructionsComponent` + attempt-shell placeholder

**Files:**
- Create: `frontend/src/app/features/candidate/instructions.component.ts`
- Create: `frontend/src/app/features/candidate/instructions.component.html`
- Create: `frontend/src/app/features/candidate/attempt-shell.component.ts`

- [ ] **Step 1: Write the instructions component**

Create `frontend/src/app/features/candidate/instructions.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CandidateExamService, CandidateIdentity, ExamLanding } from '../../core/services/candidate-exam.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';

@Component({
  selector: 'app-instructions',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './instructions.component.html'
})
export class InstructionsComponent implements OnInit {
  examId = '';
  landing: ExamLanding | null = null;
  starting = false;
  error: string | null = null;
  private identity: CandidateIdentity | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: CandidateExamService,
    private readonly tokenStore: AttemptTokenStore
  ) {}

  ngOnInit(): void {
    this.examId = this.route.parent?.snapshot.paramMap.get('examId') ?? '';
    this.identity = (history.state?.identity as CandidateIdentity) ?? null;
    if (!this.identity) {
      this.router.navigate(['../'], { relativeTo: this.route });
      return;
    }
    this.service.landing(this.examId).subscribe({ next: l => (this.landing = l) });
  }

  start(): void {
    if (this.starting || !this.identity) { return; }
    this.starting = true;
    this.error = null;
    this.service.start(this.examId, this.identity).subscribe({
      next: res => {
        this.tokenStore.set(this.examId, res.attemptToken);
        this.router.navigate(['attempt'], { relativeTo: this.route.parent });
      },
      error: () => { this.starting = false; this.error = 'تعذّر بدء الامتحان — حاول مرة أخرى.'; }
    });
  }
}
```

- [ ] **Step 2: Write the instructions template**

Create `frontend/src/app/features/candidate/instructions.component.html`:

```html
<div class="candidate-card">
  <div class="candidate-header">
    <img src="assets/moj-logo.png" alt="شعار وزارة العدل" />
    <div class="exam-title">تعليمات الامتحان</div>
  </div>

  <div class="candidate-state" *ngIf="landing">
    <p>عدد الأسئلة: <strong>{{ landing.totalQuestionCount }}</strong></p>
    <p>مدة الامتحان: <strong>{{ landing.durationMinutes }}</strong> دقيقة</p>
    <ul style="text-align: start; line-height: 1.9;">
      <li>يبدأ المؤقّت فور الضغط على «ابدأ الامتحان» ولا يتوقف.</li>
      <li>تُحفظ إجاباتك تلقائياً؛ يمكنك استئناف الامتحان بنفس بياناتك إذا انقطع الاتصال.</li>
      <li>عند انتهاء الوقت يُسلّم الامتحان تلقائياً.</li>
    </ul>
  </div>

  <p class="field-error" *ngIf="error">{{ error }}</p>

  <button class="btn-primary" type="button" (click)="start()" [disabled]="starting || !landing">
    {{ starting ? 'جارٍ البدء…' : 'ابدأ الامتحان' }}
  </button>
</div>
```

- [ ] **Step 3: Write the attempt-shell placeholder (1a)**

Create `frontend/src/app/features/candidate/attempt-shell.component.ts`:

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-attempt-shell',
  standalone: true,
  template: `
    <div class="candidate-card">
      <div class="candidate-state">
        <h1 style="font-size: var(--fs-headline);">تم بدء المحاولة</h1>
        <p class="muted">مُشغّل الأسئلة يُبنى في المرحلة التالية (Slice 1b).</p>
      </div>
    </div>
  `
})
export class AttemptShellComponent {}
```

- [ ] **Step 4: Verify the frontend builds (all lazy imports now resolve)**

Run: `cd frontend && npm run build`
Expected: Build succeeds (all candidate route `loadComponent` targets exist).

- [ ] **Step 5: Run the full frontend test suite**

Run: `cd frontend && npm test -- --watch=false`
Expected: PASS (existing + candidate specs).

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/features/candidate/instructions.component.* frontend/src/app/features/candidate/attempt-shell.component.ts
git commit -m "feat(candidate-ui): instructions screen and start -> attempt shell placeholder"
```

---

## Task 14: Logo asset + end-to-end verification

**Files:**
- Create: `frontend/src/assets/moj-logo.png` (provided emblem)

- [ ] **Step 1: Place the emblem asset**

Save the provided Ministry of Justice emblem to `frontend/src/assets/moj-logo.png`. (Until it exists, the
`<img>` shows its `alt` text — the flow still works.)

- [ ] **Step 2: Run backend + start both servers**

Run: `dotnet test ExamSystem.sln` — Expected: all pass.
Start the API (`preview_start api`) and the frontend (`preview_start frontend`, proxy already configured).

- [ ] **Step 3: Manual end-to-end check (real data)**

As admin, ensure a Published exam that is currently open exists with enough MCQs in the bank (reuse the
seed data). Copy its exam id. In the browser, open `/exam/{examId}`:
- Confirm the branded landing renders (emblem + org line + exam title).
- Enter a valid identity → "متابعة" → instructions page shows duration + question count.
- "ابدأ الامتحان" → lands on the attempt shell ("تم بدء المحاولة").
- Verify via `preview_network` that `register` and `start` returned 200 and `start` returned an `attemptToken`.
- Re-open `/exam/{examId}`, enter the same identity → "متابعة" → instructions → "ابدأ الامتحان" returns the
  same `attemptId` (resume/idempotent), and a second identical registration with a fresh national ID after a
  submitted attempt is blocked with the "أدّيت هذا الامتحان" message (checked once 1b can submit; for 1a,
  confirm the AlreadyTaken branch by inspecting the API on a candidate with an existing attempt row).

- [ ] **Step 4: Screenshot proof + commit the asset**

Capture `preview_screenshot` of the landing and instructions screens.

```bash
git add frontend/src/assets/moj-logo.png
git commit -m "feat(candidate-ui): add Ministry of Justice emblem asset"
```

---

## Self-Review — Spec Coverage

- Spec §2 boundary → Tasks 1-14 (register/validate/profile/guard/instructions/start; answering deferred to 1b). ✅
- Spec §3.1 Domain (`Candidate`/`NationalId`/`ExamAttempt`/`AttemptQuestion(+Option)`/grant) → Tasks 1-2. ✅
- Spec §3.2 Application (landing/register/start + validators + selection service) → Tasks 4-6, 8. ✅
- Spec §3.3 Infrastructure (EF configs + migration + token generator + selection) → Tasks 3, 4, 7. ✅
- Spec §3.4 API (`CandidateExamController`, 3 endpoints) → Task 9. ✅
- Spec §4 Attempt Token (separate scheme, claims, storage, resume) → Tasks 7, 8, 11. ✅
- Spec §5 Frontend (public `/exam` area, candidate theme, landing + instructions + shell, service + interceptor) → Tasks 10-13. ✅
- Spec §6 validation & error handling (national ID / mobile / 4-word name / already-taken / not-open) → Tasks 1, 6, 12. ✅
- Spec §7 testing (unit + integration + component specs, TDD) → every task. ✅
- Spec §5 visual identity (emblem, judicial green, gold accent) → Tasks 10, 14. ✅

No placeholders remain; type/name usage (`RegisterOutcome`, `StartAttemptDto`, `AttemptTokenStore`,
`attemptTokenInterceptor`, claim constants) is consistent across tasks.
