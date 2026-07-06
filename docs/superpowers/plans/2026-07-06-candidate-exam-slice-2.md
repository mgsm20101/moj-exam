# Candidate Exam — Slice 2 Implementation Plan (Batch Gate / Waiting Room, Auto mode)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cap concurrent attempts per exam; when full, place candidates in a FIFO waiting queue with a polling waiting room, and promote/start them one at a time as slots free — with no background job and no changes to the 1b engine.

**Architecture:** Clean Architecture + CQRS mirroring 1a/1b. `Exam` gains capacity fields; a new `WaitingQueueEntry` entity + `IQueueReconciler` (lazy promote/expire on each request). `StartAttemptCommand` becomes the single gate (attempt-or-enqueue), returning a widened `StartAttemptDto`. A new anonymous `GET queue/status` powers the waiting room. Active count = `InProgress` attempts whose timer hasn't expired, so finished/expired attempts free their slot implicitly.

**Tech Stack:** .NET 8, EF Core 8 (SQL Server; SQLite for tests), MediatR, FluentValidation, xUnit; Angular 17 standalone components, SCSS.

**Spec:** `docs/superpowers/specs/2026-07-06-candidate-exam-slice-2-design.md`
**Depends on:** Slices 1a + 1b (done). Reuses `ExamAttempt`, `AttemptToken`, `StartAttemptCommand`, candidate `/exam` area.

---

## Reference: existing shapes this plan modifies

- `StartAttemptDto` (1a) is currently `record StartAttemptDto(Guid AttemptId, string AttemptToken, DateTime ExpiresAtUtc)` — **widened** in Task 4.
- `StartAttemptCommandHandler` (1a) constructs it via a private `Ok(...)` helper — **modified** in Task 4.
- `ExamAttempt.Status` uses `ExamAttemptStatus` (`InProgress, Submitted, AutoSubmitted, Terminated`); active = `InProgress && ExpiresAtUtc > now`.
- `CreateExamCommand` / `UpdateExamCommand` / `ExamDetailDto` / `ExamsController.UpdateExamRequest` / `CloneExamCommandHandler` carry the exam config; admin exam form is `frontend/src/app/features/admin/exams/exam-form.component.{ts,html}` with `exam.service.ts` DTOs.

---

## File Structure

**Backend — Domain**
- `Exams/Exam.cs` (modify — 2 fields)
- `Queue/WaitingQueueEntry.cs`, `Queue/WaitingQueueStatus.cs` (new)

**Backend — Application**
- `Common/Interfaces/IApplicationDbContext.cs` (modify — DbSet)
- `Common/Interfaces/IQueueReconciler.cs` + `Common/Models/QueueCapacity.cs` (new)
- `Features/CandidateExam/StartAttempt/StartAttemptDto.cs` + `StartAttemptCommandHandler.cs` (modify)
- `Features/CandidateExam/Queue/QueueStatusDto.cs`, `GetQueueStatusQuery.cs`, `GetQueueStatusQueryHandler.cs` (new)
- Admin exam config (modify): `Features/Exams/CreateExam/{CreateExamCommand,CreateExamCommandHandler,CreateExamCommandValidator}.cs`, `Features/Exams/UpdateExam/{UpdateExamCommand,UpdateExamCommandHandler}.cs`, `Features/Exams/GetExamById/{ExamDetailDto,GetExamByIdQueryHandler}.cs`, `Features/Exams/CloneExam/CloneExamCommandHandler.cs`

**Backend — Infrastructure**
- `Persistence/{ApplicationDbContext}.cs` (modify), `Persistence/Configurations/WaitingQueueEntryConfiguration.cs` (new)
- `Queue/QueueReconciler.cs` (new), `DependencyInjection.cs` (modify), `Migrations/*` (generated)

**Backend — API**
- `Controllers/CandidateQueueController.cs` (new)
- `Controllers/ExamsController.cs` (modify — `UpdateExamRequest`)

**Backend — Tests**
- `tests/ExamSystem.Application.UnitTests/Queue/QueueReconcilerTests.cs`
- `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptQueueingTests.cs`
- `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetQueueStatusQueryHandlerTests.cs`
- `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateQueueControllerTests.cs`

**Frontend**
- `core/services/candidate-exam.service.ts` (modify — widened `start`, `queueStatus`)
- `features/candidate/instructions.component.ts` (modify — branch on outcome)
- `features/candidate/waiting-room.component.ts` (+ `.html`, `.spec.ts`) (new)
- `features/candidate/candidate.routes.ts` (modify — `waiting` route)
- `features/admin/exams/exam-form.component.{ts,html}` + `core/services/exam.service.ts` (modify — capacity fields)
- `styles/_candidate.scss` (modify — waiting styles)

---

## Task 1: `Exam` capacity fields + `WaitingQueueEntry` entity

**Files:**
- Modify: `src/ExamSystem.Domain/Exams/Exam.cs`
- Create: `src/ExamSystem.Domain/Queue/WaitingQueueStatus.cs`
- Create: `src/ExamSystem.Domain/Queue/WaitingQueueEntry.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Domain/QueueEntityDefaultsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/ExamSystem.Application.UnitTests/Domain/QueueEntityDefaultsTests.cs`:

```csharp
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using Xunit;

namespace ExamSystem.Application.UnitTests.Domain;

public class QueueEntityDefaultsTests
{
    [Fact]
    public void Exam_Defaults_HaveCapacityAndGrace()
    {
        var exam = new Exam();
        Assert.Equal(20, exam.MaxConcurrentAttempts);
        Assert.Equal(3, exam.GraceWindowMinutes);
    }

    [Fact]
    public void WaitingQueueEntry_Defaults_AreWaiting()
    {
        var entry = new WaitingQueueEntry();
        Assert.Equal(WaitingQueueStatus.Waiting, entry.Status);
        Assert.NotEqual(System.Guid.Empty, entry.Id);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~QueueEntityDefaultsTests`
Expected: FAIL — `Queue` namespace / fields do not exist.

- [ ] **Step 3: Add the Exam fields**

Modify `src/ExamSystem.Domain/Exams/Exam.cs` — add after `public int MaxAttempts { get; set; } = 1;`:

```csharp
    /// <summary>Batch-gate capacity (FR-8.1): max concurrent InProgress attempts before queueing.</summary>
    public int MaxConcurrentAttempts { get; set; } = 20;

    /// <summary>Minutes a called candidate has to start before their reserved slot is released (FR-8.5).</summary>
    public int GraceWindowMinutes { get; set; } = 3;
```

- [ ] **Step 4: Create the queue entity + status**

Create `src/ExamSystem.Domain/Queue/WaitingQueueStatus.cs`:

```csharp
namespace ExamSystem.Domain.Queue;

public enum WaitingQueueStatus
{
    Waiting = 0,
    Called = 1,
    Started = 2,
    Expired = 3,
    Cancelled = 4
}
```

Create `src/ExamSystem.Domain/Queue/WaitingQueueEntry.cs`:

```csharp
namespace ExamSystem.Domain.Queue;

/// <summary>A candidate's place in an exam's FIFO batch-gate queue (FR-8), keyed by candidate per exam.</summary>
public class WaitingQueueEntry : BaseEntity
{
    public Guid ExamId { get; set; }
    public Guid CandidateId { get; set; }

    public DateTime EnqueuedAtUtc { get; set; }
    public int Position { get; set; }
    public DateTime? CalledAtUtc { get; set; }

    public WaitingQueueStatus Status { get; set; } = WaitingQueueStatus.Waiting;
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~QueueEntityDefaultsTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Domain/Exams/Exam.cs src/ExamSystem.Domain/Queue/ tests/ExamSystem.Application.UnitTests/Domain/QueueEntityDefaultsTests.cs
git commit -m "feat(domain): add exam capacity fields and WaitingQueueEntry"
```

---

## Task 2: Persistence — DbSet, configuration, migration

**Files:**
- Modify: `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs`
- Create: `src/ExamSystem.Infrastructure/Persistence/Configurations/WaitingQueueEntryConfiguration.cs`

- [ ] **Step 1: Add the DbSet to the interface**

Modify `src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs` — add the using and the member:

```csharp
using ExamSystem.Domain.Queue;
```

Add after `DbSet<AttemptAnswer> AttemptAnswers { get; }`:

```csharp
    DbSet<WaitingQueueEntry> WaitingQueueEntries { get; }
```

- [ ] **Step 2: Add the DbSet to the context**

Modify `src/ExamSystem.Infrastructure/Persistence/ApplicationDbContext.cs` — add the using:

```csharp
using ExamSystem.Domain.Queue;
```

Add after `public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();`:

```csharp
    public DbSet<WaitingQueueEntry> WaitingQueueEntries => Set<WaitingQueueEntry>();
```

- [ ] **Step 3: Write the configuration**

Create `src/ExamSystem.Infrastructure/Persistence/Configurations/WaitingQueueEntryConfiguration.cs`:

```csharp
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
```

- [ ] **Step 4: Verify build**

Run: `dotnet build ExamSystem.sln`
Expected: Build succeeded.

- [ ] **Step 5: Generate the migration**

Run:
```bash
dotnet ef migrations add AddWaitingQueueAndCapacity \
  --project src/ExamSystem.Infrastructure \
  --startup-project src/ExamSystem.Api \
  --output-dir Migrations
```
Expected: a migration creating `WaitingQueueEntries` and adding `MaxConcurrentAttempts` + `GraceWindowMinutes` columns to `Exams`. Open it and confirm.

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Application/Common/Interfaces/IApplicationDbContext.cs src/ExamSystem.Infrastructure/Persistence/ src/ExamSystem.Infrastructure/Migrations/
git commit -m "feat(infra): persist WaitingQueueEntry + exam capacity columns + migration"
```

---

## Task 3: `IQueueReconciler`

**Files:**
- Create: `src/ExamSystem.Application/Common/Models/QueueCapacity.cs`
- Create: `src/ExamSystem.Application/Common/Interfaces/IQueueReconciler.cs`
- Create: `src/ExamSystem.Infrastructure/Queue/QueueReconciler.cs`
- Modify: `src/ExamSystem.Infrastructure/DependencyInjection.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Queue/QueueReconcilerTests.cs`

- [ ] **Step 1: Define the model + interface**

Create `src/ExamSystem.Application/Common/Models/QueueCapacity.cs`:

```csharp
namespace ExamSystem.Application.Common.Models;

/// <summary>Post-reconciliation capacity snapshot for an exam.</summary>
public record QueueCapacity(int MaxConcurrent, int ActiveAttempts, int Reserved)
{
    public int Available => Math.Max(0, MaxConcurrent - ActiveAttempts - Reserved);
}
```

Create `src/ExamSystem.Application/Common/Interfaces/IQueueReconciler.cs`:

```csharp
namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Lazy batch-gate reconciliation (FR-8): expire grace-timed-out Called reservations, promote the
/// earliest Waiting candidates while capacity allows, and recompute Waiting positions. Runs on every
/// start / queue-status request; returns the post-reconciliation capacity. Persists its changes.
/// </summary>
public interface IQueueReconciler
{
    Task<QueueCapacity> ReconcileAsync(Guid examId, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/Queue/QueueReconcilerTests.cs`:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Queue;

public class QueueReconcilerTests
{
    private static Exam Exam(int max, int grace = 3) =>
        new() { Name = "E", DurationMinutes = 60, MaxConcurrentAttempts = max, GraceWindowMinutes = grace };

    private static ExamAttempt InProgress(Guid examId, DateTime expiresAtUtc) => new()
    {
        ExamId = examId, CandidateId = Guid.NewGuid(),
        StartedAtUtc = DateTime.UtcNow.AddMinutes(-1), ExpiresAtUtc = expiresAtUtc,
        Status = ExamAttemptStatus.InProgress
    };

    private static WaitingQueueEntry Waiting(Guid examId, DateTime enqueuedAtUtc) => new()
    {
        ExamId = examId, CandidateId = Guid.NewGuid(), EnqueuedAtUtc = enqueuedAtUtc,
        Status = WaitingQueueStatus.Waiting
    };

    [Fact]
    public async Task Reconcile_FreeSlot_PromotesEarliestWaiting()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1);
        db.Exams.Add(exam);
        var early = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-5));
        var late = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-2));
        db.WaitingQueueEntries.AddRange(late, early); // insert out of order on purpose
        await db.SaveChangesAsync(CancellationToken.None);

        var capacity = await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        var promoted = db.WaitingQueueEntries.Single(e => e.Status == WaitingQueueStatus.Called);
        Assert.Equal(early.Id, promoted.Id);                 // FIFO
        Assert.NotNull(promoted.CalledAtUtc);
        Assert.Equal(1, db.WaitingQueueEntries.Count(e => e.Status == WaitingQueueStatus.Waiting));
        Assert.Equal(0, capacity.Available);                 // slot now reserved
    }

    [Fact]
    public async Task Reconcile_ActiveAttemptFillsCapacity_NoPromotion()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1);
        db.Exams.Add(exam);
        db.ExamAttempts.Add(InProgress(exam.Id, DateTime.UtcNow.AddMinutes(30)));
        db.WaitingQueueEntries.Add(Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync(CancellationToken.None);

        var capacity = await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        Assert.Equal(0, db.WaitingQueueEntries.Count(e => e.Status == WaitingQueueStatus.Called));
        Assert.Equal(1, capacity.ActiveAttempts);
    }

    [Fact]
    public async Task Reconcile_ExpiredAttempt_DoesNotCount_AndPromotes()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1);
        db.Exams.Add(exam);
        db.ExamAttempts.Add(InProgress(exam.Id, DateTime.UtcNow.AddMinutes(-1))); // timer already expired
        db.WaitingQueueEntries.Add(Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync(CancellationToken.None);

        await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        Assert.Equal(1, db.WaitingQueueEntries.Count(e => e.Status == WaitingQueueStatus.Called));
    }

    [Fact]
    public async Task Reconcile_ExpiredCalledPastGrace_ReleasesSlotToNext()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1, grace: 3);
        db.Exams.Add(exam);
        var stale = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-10));
        stale.Status = WaitingQueueStatus.Called;
        stale.CalledAtUtc = DateTime.UtcNow.AddMinutes(-5); // past 3-min grace
        var next = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-4));
        db.WaitingQueueEntries.AddRange(stale, next);
        await db.SaveChangesAsync(CancellationToken.None);

        await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        Assert.Equal(WaitingQueueStatus.Expired, db.WaitingQueueEntries.Single(e => e.Id == stale.Id).Status);
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == next.Id).Status);
    }

    [Fact]
    public async Task Reconcile_RecomputesWaitingPositions()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 0); // no capacity -> nothing promoted
        db.Exams.Add(exam);
        var first = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-5));
        var second = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-3));
        db.WaitingQueueEntries.AddRange(second, first);
        await db.SaveChangesAsync(CancellationToken.None);

        await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        Assert.Equal(1, db.WaitingQueueEntries.Single(e => e.Id == first.Id).Position);
        Assert.Equal(2, db.WaitingQueueEntries.Single(e => e.Id == second.Id).Position);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~QueueReconcilerTests`
Expected: FAIL — `QueueReconciler` does not exist.

- [ ] **Step 4: Implement the reconciler**

Create `src/ExamSystem.Infrastructure/Queue/QueueReconciler.cs`:

```csharp
using ExamSystem.Application.Common.Interfaces;
using ExamSystem.Application.Common.Models;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Queue;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Infrastructure.Queue;

public class QueueReconciler : IQueueReconciler
{
    private readonly IApplicationDbContext _db;

    public QueueReconciler(IApplicationDbContext db) => _db = db;

    public async Task<QueueCapacity> ReconcileAsync(Guid examId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var exam = await _db.Exams.FirstAsync(e => e.Id == examId, cancellationToken);

        // 1. Expire Called reservations whose grace window has elapsed.
        var called = await _db.WaitingQueueEntries
            .Where(e => e.ExamId == examId && e.Status == WaitingQueueStatus.Called)
            .ToListAsync(cancellationToken);
        foreach (var entry in called)
        {
            if (entry.CalledAtUtc is { } calledAt && calledAt.AddMinutes(exam.GraceWindowMinutes) <= now)
            {
                entry.Status = WaitingQueueStatus.Expired;
            }
        }

        var activeAttempts = await _db.ExamAttempts.CountAsync(
            a => a.ExamId == examId && a.Status == ExamAttemptStatus.InProgress && a.ExpiresAtUtc > now,
            cancellationToken);
        var reserved = called.Count(e => e.Status == WaitingQueueStatus.Called);

        // 2. Promote earliest Waiting while capacity allows.
        var waiting = await _db.WaitingQueueEntries
            .Where(e => e.ExamId == examId && e.Status == WaitingQueueStatus.Waiting)
            .OrderBy(e => e.EnqueuedAtUtc)
            .ToListAsync(cancellationToken);

        var available = Math.Max(0, exam.MaxConcurrentAttempts - activeAttempts - reserved);
        var index = 0;
        for (; index < waiting.Count && available > 0; index++, available--, reserved++)
        {
            waiting[index].Status = WaitingQueueStatus.Called;
            waiting[index].CalledAtUtc = now;
        }

        // 3. Recompute positions for those still Waiting.
        var position = 1;
        for (var i = index; i < waiting.Count; i++)
        {
            waiting[i].Position = position++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new QueueCapacity(exam.MaxConcurrentAttempts, activeAttempts, reserved);
    }
}
```

- [ ] **Step 5: Register the service**

Modify `src/ExamSystem.Infrastructure/DependencyInjection.cs` — add the using:

```csharp
using ExamSystem.Infrastructure.Queue;
```

Add before `return services;`:

```csharp
        services.AddScoped<IQueueReconciler, QueueReconciler>();
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~QueueReconcilerTests`
Expected: PASS (5 tests).

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application/Common/Models/QueueCapacity.cs src/ExamSystem.Application/Common/Interfaces/IQueueReconciler.cs src/ExamSystem.Infrastructure/Queue/ src/ExamSystem.Infrastructure/DependencyInjection.cs tests/ExamSystem.Application.UnitTests/Queue/
git commit -m "feat: lazy queue reconciler (promote/expire/positions) for the batch gate"
```

---

## Task 4: Gate `StartAttemptCommand` (attempt-or-enqueue)

**Files:**
- Modify: `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptDto.cs`
- Modify: `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommandHandler.cs`
- Modify: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptCommandHandlerTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptQueueingTests.cs`

- [ ] **Step 1: Widen the DTO**

Replace `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptDto.cs` with:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

/// <summary>Outcome of pressing "start": either an attempt was created, or the candidate was queued.</summary>
public record StartAttemptDto(
    string Outcome,               // "Started" | "Queued"
    Guid? AttemptId,
    string? AttemptToken,
    DateTime? ExpiresAtUtc,
    int? QueuePosition)
{
    public static StartAttemptDto Started(Guid attemptId, string token, DateTime expiresAtUtc) =>
        new("Started", attemptId, token, expiresAtUtc, null);

    public static StartAttemptDto Queued(int position) =>
        new("Queued", null, null, null, position);
}
```

- [ ] **Step 2: Update the existing StartAttempt unit tests to the new shape**

In `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptCommandHandlerTests.cs`, the
handler now needs an `IQueueReconciler`. Add a fake and pass it, and assert the `Started` outcome. Replace the
`FakeTokenGenerator` block and each `new StartAttemptCommandHandler(db, new QuestionSelectionService(db), new FakeTokenGenerator())`
construction accordingly. Add this fake reconciler class inside the test class:

```csharp
    private sealed class FakeReconciler : ExamSystem.Application.Common.Interfaces.IQueueReconciler
    {
        // No queue in these tests: capacity is always available.
        public Task<ExamSystem.Application.Common.Models.QueueCapacity> ReconcileAsync(System.Guid examId, System.Threading.CancellationToken ct)
            => Task.FromResult(new ExamSystem.Application.Common.Models.QueueCapacity(20, 0, 0));
    }
```

Change every handler construction to:

```csharp
        var handler = new StartAttemptCommandHandler(db, new QuestionSelectionService(db), new FakeTokenGenerator(), new FakeReconciler());
```

And in `Handle_NewCandidate_CreatesAttemptWithSnapshotTimerAndToken`, after the existing asserts, add:

```csharp
        Assert.Equal("Started", result.Value!.Outcome);
```

- [ ] **Step 3: Write the failing queueing tests**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptQueueingTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.StartAttempt;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Domain.Questions;
using ExamSystem.Domain.Topics;
using ExamSystem.Infrastructure.Grading;
using ExamSystem.Infrastructure.Queue;
using ExamSystem.Infrastructure.Selection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class StartAttemptQueueingTests
{
    private const string Nid = "29912310123404";
    private const string Nid2 = "30106152112354"; // valid male id, different candidate

    private sealed class FakeTokenGenerator : ExamSystem.Application.Common.Interfaces.IAttemptTokenGenerator
    {
        public string GenerateToken(Guid a, Guid c, Guid e, DateTime x) => $"token-{a}";
    }

    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, Exam exam)> SeedAsync(int max)
    {
        var db = TestDbContextFactory.Create();
        var topic = new Topic { Name = "T", DisplayOrder = 1 };
        db.Topics.Add(topic);
        for (var i = 0; i < 3; i++)
        {
            db.Questions.Add(new Question
            {
                TopicId = topic.Id, Type = QuestionType.Mcq, Difficulty = DifficultyLevel.Medium,
                Text = "Q", IsActive = true,
                Options = new List<QuestionOption> { new() { Text = "A", IsCorrect = true, DisplayOrder = 1 } }
            });
        }
        var exam = new Exam
        {
            Name = "E", DurationMinutes = 60, Status = ExamStatus.Published, MaxConcurrentAttempts = max,
            StartAtUtc = DateTime.UtcNow.AddHours(-1), EndAtUtc = DateTime.UtcNow.AddHours(1)
        };
        exam.TopicSelections.Add(new ExamTopicSelection
        { TopicId = topic.Id, DisplayOrder = 1, Difficulty = DifficultyLevel.Medium, Type = QuestionType.Mcq, Count = 1 });
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, exam);
    }

    private static StartAttemptCommandHandler Handler(Infrastructure.Persistence.ApplicationDbContext db) =>
        new(db, new QuestionSelectionService(db), new FakeTokenGenerator(), new QueueReconciler(db));

    [Fact]
    public async Task Start_AtCapacity_QueuesSecondCandidate()
    {
        var (db, exam) = await SeedAsync(max: 1);

        var first = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);
        var second = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);

        Assert.Equal("Started", first.Value!.Outcome);
        Assert.Equal("Queued", second.Value!.Outcome);
        Assert.Equal(1, second.Value.QueuePosition);
        Assert.Single(db.WaitingQueueEntries.Where(e => e.Status == WaitingQueueStatus.Waiting));
    }

    [Fact]
    public async Task Start_AfterSlotFrees_StartsQueuedCandidate()
    {
        var (db, exam) = await SeedAsync(max: 1);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);

        // first candidate submits -> their attempt is no longer InProgress
        var attempt = db.ExamAttempts.Single();
        attempt.Status = ExamAttemptStatus.Submitted;
        await db.SaveChangesAsync(CancellationToken.None);

        // queued candidate presses start again -> now gets an attempt
        var retry = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);

        Assert.Equal("Started", retry.Value!.Outcome);
        Assert.False(string.IsNullOrEmpty(retry.Value.AttemptToken));
    }

    [Fact]
    public async Task Start_RepeatedWhileQueued_IsIdempotent()
    {
        var (db, exam) = await SeedAsync(max: 1);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "منى سمير علي حسن", Nid2, "01112345678"), CancellationToken.None);

        Assert.Single(db.WaitingQueueEntries); // no duplicate queue entry
    }
}
```

- [ ] **Step 4: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~StartAttemptQueueingTests`
Expected: FAIL — the handler constructor does not yet take an `IQueueReconciler`.

- [ ] **Step 5: Rewrite the handler with the gate**

Replace `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommandHandler.cs` with:

```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.CandidateExam.StartAttempt;

public class StartAttemptCommandHandler : IRequestHandler<StartAttemptCommand, Result<StartAttemptDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQuestionSelectionService _selection;
    private readonly IAttemptTokenGenerator _tokens;
    private readonly IQueueReconciler _reconciler;

    public StartAttemptCommandHandler(
        IApplicationDbContext db, IQuestionSelectionService selection,
        IAttemptTokenGenerator tokens, IQueueReconciler reconciler)
    {
        _db = db;
        _selection = selection;
        _tokens = tokens;
        _reconciler = reconciler;
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

        // Resume: an in-progress attempt is returned as-is.
        var existing = await _db.ExamAttempts.FirstOrDefaultAsync(
            a => a.ExamId == exam.Id && a.CandidateId == candidate.Id && a.Status == ExamAttemptStatus.InProgress,
            cancellationToken);
        if (existing is not null)
        {
            return Result<StartAttemptDto>.Success(Token(existing, candidate.Id, exam.Id));
        }

        // Already taken (no active grant) blocks before any queueing.
        var hasAnyAttempt = await _db.ExamAttempts.AnyAsync(
            a => a.ExamId == exam.Id && a.CandidateId == candidate.Id, cancellationToken);
        var hasActiveGrant = await _db.CandidateExamAttemptGrants.AnyAsync(
            g => g.ExamId == exam.Id && g.CandidateId == candidate.Id && g.IsActive, cancellationToken);
        if (hasAnyAttempt && !hasActiveGrant)
        {
            return Result<StartAttemptDto>.Failure("You have already taken this exam.");
        }

        // Batch gate.
        var capacity = await _reconciler.ReconcileAsync(exam.Id, cancellationToken);

        var called = await _db.WaitingQueueEntries.FirstOrDefaultAsync(
            e => e.ExamId == exam.Id && e.CandidateId == candidate.Id && e.Status == WaitingQueueStatus.Called,
            cancellationToken);

        if (called is not null || capacity.Available > 0)
        {
            var attempt = await CreateAttemptAsync(exam, candidate.Id, now, cancellationToken);
            if (!attempt.IsSuccess)
            {
                return Result<StartAttemptDto>.Failure(attempt.Errors);
            }

            // Mark any queue entry for this candidate as Started.
            var mine = await _db.WaitingQueueEntries.Where(
                e => e.ExamId == exam.Id && e.CandidateId == candidate.Id
                     && (e.Status == WaitingQueueStatus.Waiting || e.Status == WaitingQueueStatus.Called))
                .ToListAsync(cancellationToken);
            foreach (var entry in mine) { entry.Status = WaitingQueueStatus.Started; }
            await _db.SaveChangesAsync(cancellationToken);

            return Result<StartAttemptDto>.Success(Token(attempt.Value!, candidate.Id, exam.Id));
        }

        // Enqueue (idempotent).
        var waiting = await _db.WaitingQueueEntries.FirstOrDefaultAsync(
            e => e.ExamId == exam.Id && e.CandidateId == candidate.Id && e.Status == WaitingQueueStatus.Waiting,
            cancellationToken);
        if (waiting is null)
        {
            waiting = new WaitingQueueEntry
            {
                ExamId = exam.Id, CandidateId = candidate.Id, EnqueuedAtUtc = now,
                Status = WaitingQueueStatus.Waiting
            };
            _db.WaitingQueueEntries.Add(waiting);
            await _db.SaveChangesAsync(cancellationToken);
            await _reconciler.ReconcileAsync(exam.Id, cancellationToken); // assign position
            waiting = await _db.WaitingQueueEntries.FirstAsync(e => e.Id == waiting.Id, cancellationToken);
        }

        return Result<StartAttemptDto>.Success(StartAttemptDto.Queued(waiting.Position));
    }

    private async Task<Result<ExamAttempt>> CreateAttemptAsync(Exam exam, Guid candidateId, DateTime now, CancellationToken cancellationToken)
    {
        var attempt = new ExamAttempt
        {
            ExamId = exam.Id,
            CandidateId = candidateId,
            StartedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(exam.DurationMinutes),
            Status = ExamAttemptStatus.InProgress
        };
        attempt.Seed = attempt.Id.GetHashCode();

        var snapshot = await _selection.BuildSnapshotAsync(exam, attempt.Seed, cancellationToken);
        if (!snapshot.IsSuccess)
        {
            return Result<ExamAttempt>.Failure(snapshot.Errors);
        }
        foreach (var q in snapshot.Value!) { attempt.Questions.Add(q); }

        _db.ExamAttempts.Add(attempt);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<ExamAttempt>.Success(attempt);
    }

    private StartAttemptDto Token(ExamAttempt attempt, Guid candidateId, Guid examId)
    {
        var token = _tokens.GenerateToken(attempt.Id, candidateId, examId, attempt.ExpiresAtUtc);
        return StartAttemptDto.Started(attempt.Id, token, attempt.ExpiresAtUtc);
    }
}
```

- [ ] **Step 6: Run to verify both test files pass**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter "FullyQualifiedName~StartAttempt"`
Expected: PASS (existing StartAttempt tests updated + new queueing tests).

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/StartAttempt/ tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttempt*
git commit -m "feat(app): gate StartAttempt behind the batch capacity (attempt-or-enqueue)"
```

---

## Task 5: `GetQueueStatusQuery` + `CandidateQueueController` + integration

**Files:**
- Create: `src/ExamSystem.Application/Features/CandidateExam/Queue/QueueStatusDto.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/Queue/GetQueueStatusQuery.cs`
- Create: `src/ExamSystem.Application/Features/CandidateExam/Queue/GetQueueStatusQueryHandler.cs`
- Create: `src/ExamSystem.Api/Controllers/CandidateQueueController.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetQueueStatusQueryHandlerTests.cs`
- Test: `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateQueueControllerTests.cs`

- [ ] **Step 1: Write the DTO + query**

Create `src/ExamSystem.Application/Features/CandidateExam/Queue/QueueStatusDto.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.Queue;

public record QueueStatusDto(string Status, int Position, int EstimatedWaitSeconds);
```

Create `src/ExamSystem.Application/Features/CandidateExam/Queue/GetQueueStatusQuery.cs`:

```csharp
namespace ExamSystem.Application.Features.CandidateExam.Queue;

public record GetQueueStatusQuery(Guid ExamId, string NationalId) : IRequest<Result<QueueStatusDto>>;
```

- [ ] **Step 2: Write the failing unit tests**

Create `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetQueueStatusQueryHandlerTests.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.Queue;
using ExamSystem.Domain.Candidates;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Infrastructure.Queue;
using Xunit;

namespace ExamSystem.Application.UnitTests.Features.CandidateExam;

public class GetQueueStatusQueryHandlerTests
{
    private const string Nid = "29912310123404";

    private static async Task<(Infrastructure.Persistence.ApplicationDbContext db, Exam exam, Candidate candidate)> SeedAsync(int max)
    {
        var db = TestDbContextFactory.Create();
        var exam = new Exam { Name = "E", DurationMinutes = 60, MaxConcurrentAttempts = max };
        var candidate = new Candidate { NationalId = Nid, FullName = "x", MobileNumber = "01012345678" };
        db.Exams.Add(exam);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(CancellationToken.None);
        return (db, exam, candidate);
    }

    [Fact]
    public async Task Handle_WaitingCandidate_ReturnsPosition()
    {
        var (db, exam, candidate) = await SeedAsync(max: 0); // no capacity -> stays waiting
        db.WaitingQueueEntries.Add(new WaitingQueueEntry
        { ExamId = exam.Id, CandidateId = candidate.Id, EnqueuedAtUtc = DateTime.UtcNow.AddMinutes(-1), Status = WaitingQueueStatus.Waiting });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetQueueStatusQueryHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new GetQueueStatusQuery(exam.Id, Nid), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Waiting", result.Value!.Status);
        Assert.Equal(1, result.Value.Position);
    }

    [Fact]
    public async Task Handle_SlotAvailable_PromotesToCalled()
    {
        var (db, exam, candidate) = await SeedAsync(max: 1); // capacity -> reconcile promotes
        db.WaitingQueueEntries.Add(new WaitingQueueEntry
        { ExamId = exam.Id, CandidateId = candidate.Id, EnqueuedAtUtc = DateTime.UtcNow.AddMinutes(-1), Status = WaitingQueueStatus.Waiting });
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetQueueStatusQueryHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new GetQueueStatusQuery(exam.Id, Nid), CancellationToken.None);

        Assert.Equal("Called", result.Value!.Status);
    }

    [Fact]
    public async Task Handle_UnknownCandidate_ReturnsNotQueued()
    {
        var (db, exam, _) = await SeedAsync(max: 5);

        var handler = new GetQueueStatusQueryHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new GetQueueStatusQuery(exam.Id, "30106152112354"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("NotQueued", result.Value!.Status);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~GetQueueStatusQueryHandlerTests`
Expected: FAIL — handler does not exist.

- [ ] **Step 4: Implement the handler**

Create `src/ExamSystem.Application/Features/CandidateExam/Queue/GetQueueStatusQueryHandler.cs`:

```csharp
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.CandidateExam.Queue;

public class GetQueueStatusQueryHandler : IRequestHandler<GetQueueStatusQuery, Result<QueueStatusDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueueReconciler _reconciler;

    public GetQueueStatusQueryHandler(IApplicationDbContext db, IQueueReconciler reconciler)
    {
        _db = db;
        _reconciler = reconciler;
    }

    public async Task<Result<QueueStatusDto>> Handle(GetQueueStatusQuery request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<QueueStatusDto>.Failure("Exam not found.");
        }

        await _reconciler.ReconcileAsync(request.ExamId, cancellationToken);

        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.NationalId == request.NationalId, cancellationToken);
        if (candidate is null)
        {
            return Result<QueueStatusDto>.Success(new QueueStatusDto("NotQueued", 0, 0));
        }

        var entry = await _db.WaitingQueueEntries
            .Where(e => e.ExamId == request.ExamId && e.CandidateId == candidate.Id)
            .OrderByDescending(e => e.EnqueuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (entry is null)
        {
            return Result<QueueStatusDto>.Success(new QueueStatusDto("NotQueued", 0, 0));
        }

        var estimate = entry.Status == WaitingQueueStatus.Waiting
            ? (int)Math.Ceiling((double)entry.Position / Math.Max(1, exam.MaxConcurrentAttempts)) * exam.DurationMinutes * 60
            : 0;

        return Result<QueueStatusDto>.Success(new QueueStatusDto(entry.Status.ToString(), entry.Position, estimate));
    }
}
```

- [ ] **Step 5: Run the unit tests**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter FullyQualifiedName~GetQueueStatusQueryHandlerTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Write the controller**

Create `src/ExamSystem.Api/Controllers/CandidateQueueController.cs`:

```csharp
using ExamSystem.Application.Features.CandidateExam.Queue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Api.Controllers;

/// <summary>Public waiting-room polling (Slice 2). Identified by national ID so it survives disconnect (FR-8.6).</summary>
[ApiController]
[Route("api/exam/{examId:guid}/queue")]
[AllowAnonymous]
public class CandidateQueueController : ControllerBase
{
    private readonly ISender _sender;

    public CandidateQueueController(ISender sender) => _sender = sender;

    [HttpGet("status")]
    public async Task<IActionResult> Status(Guid examId, [FromQuery] string nationalId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetQueueStatusQuery(examId, nationalId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { errors = result.Errors });
    }
}
```

- [ ] **Step 7: Write the failing integration test**

Create `tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateQueueControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ExamSystem.Api.IntegrationTests.Controllers;

public class CandidateQueueControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    public CandidateQueueControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    private static object A => new { fullName = "احمد محمد علي حسن", nationalId = "29912310123404", mobileNumber = "01012345678" };
    private static object B => new { fullName = "منى سمير علي حسن", nationalId = "30106152112354", mobileNumber = "01112345678" };

    // Published, open exam with MaxConcurrentAttempts = 1 and a 2-MCQ bank / selection of 1.
    private static async Task<Guid> CreateCappedExamAsync(HttpClient admin)
    {
        var topicResp = await admin.PostAsJsonAsync("/api/admin/topics", new { name = $"T{Guid.NewGuid():N}", displayOrder = 1 });
        topicResp.EnsureSuccessStatusCode();
        var topicId = (await topicResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        for (var i = 0; i < 2; i++)
        {
            (await admin.PostAsJsonAsync("/api/admin/questions", new
            {
                topicId, type = "Mcq", difficulty = "Medium", text = $"Q{i}",
                options = new[] { new { text = "a", isCorrect = false }, new { text = "b", isCorrect = true } }
            })).EnsureSuccessStatusCode();
        }
        var examResp = await admin.PostAsJsonAsync("/api/admin/exams", new
        {
            name = $"Exam {Guid.NewGuid():N}", description = (string?)null,
            startAtUtc = DateTime.UtcNow.AddMinutes(-5), endAtUtc = DateTime.UtcNow.AddHours(2),
            durationMinutes = 60, mcqPoints = 2m, trueFalsePoints = 1m, fillBlankPoints = 5m,
            passMarkPercentage = 60m, maxAttempts = 1, maxConcurrentAttempts = 1, graceWindowMinutes = 3,
            shuffleAnswers = true, showResultImmediately = true, allowBackNavigation = true,
            topicSelections = new[] { new { topicId, displayOrder = 1, difficulty = "Medium", type = "Mcq", count = 1 } }
        });
        examResp.EnsureSuccessStatusCode();
        var examId = (await examResp.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        (await admin.PostAsync($"/api/admin/exams/{examId}/publish", null)).EnsureSuccessStatusCode();
        return examId;
    }

    [Fact]
    public async Task SecondCandidate_IsQueued_ThenStartsAfterFirstSubmits()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var examId = await CreateCappedExamAsync(admin);
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        var startA = await (await clientA.PostAsJsonAsync($"/api/exam/{examId}/start", A)).Content.ReadFromJsonAsync<StartResponse>();
        Assert.Equal("Started", startA!.Outcome);

        var startB = await (await clientB.PostAsJsonAsync($"/api/exam/{examId}/start", B)).Content.ReadFromJsonAsync<StartResponse>();
        Assert.Equal("Queued", startB!.Outcome);
        Assert.Equal(1, startB.QueuePosition);

        // B polls: still waiting while A holds the only slot.
        var poll1 = await (await clientB.GetAsync($"/api/exam/{examId}/queue/status?nationalId=30106152112354")).Content.ReadFromJsonAsync<StatusResponse>();
        Assert.Equal("Waiting", poll1!.Status);

        // A submits -> frees the slot. (A has a token; fetch state then submit.)
        clientA.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", startA.AttemptToken!);
        (await clientA.PostAsync($"/api/exam/{examId}/attempt/submit", null)).EnsureSuccessStatusCode();

        // B polls again -> promoted to Called.
        var poll2 = await (await clientB.GetAsync($"/api/exam/{examId}/queue/status?nationalId=30106152112354")).Content.ReadFromJsonAsync<StatusResponse>();
        Assert.Equal("Called", poll2!.Status);

        // B starts -> now gets an attempt.
        var startB2 = await (await clientB.PostAsJsonAsync($"/api/exam/{examId}/start", B)).Content.ReadFromJsonAsync<StartResponse>();
        Assert.Equal("Started", startB2!.Outcome);
    }

    private record IdResponse(Guid Id);
    private record StartResponse(string Outcome, Guid? AttemptId, string? AttemptToken, DateTime? ExpiresAtUtc, int? QueuePosition);
    private record StatusResponse(string Status, int Position, int EstimatedWaitSeconds);
}
```

- [ ] **Step 8: Run to verify it passes, then the full backend suite**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter FullyQualifiedName~CandidateQueueControllerTests`
Expected: PASS (1 test).
Run: `dotnet test ExamSystem.sln`
Expected: all pass (1a + 1b + Slice 2). The existing 1a `CandidateExamControllerTests.Start_ThenStartAgain_IsIdempotentAndReturnsToken` still passes because a default-capacity exam (20) yields `Started` with the token populated.

- [ ] **Step 9: Commit**

```bash
git add src/ExamSystem.Application/Features/CandidateExam/Queue/ src/ExamSystem.Api/Controllers/CandidateQueueController.cs tests/ExamSystem.Application.UnitTests/Features/CandidateExam/GetQueueStatusQueryHandlerTests.cs tests/ExamSystem.Api.IntegrationTests/Controllers/CandidateQueueControllerTests.cs
git commit -m "feat(api): queue status endpoint + waiting-room polling query"
```

---

## Task 6: Admin capacity fields (exam config wiring)

**Files:**
- Modify: `src/ExamSystem.Application/Features/Exams/CreateExam/CreateExamCommand.cs`, `CreateExamCommandHandler.cs`, `CreateExamCommandValidator.cs`
- Modify: `src/ExamSystem.Application/Features/Exams/UpdateExam/UpdateExamCommand.cs`, `UpdateExamCommandHandler.cs`
- Modify: `src/ExamSystem.Application/Features/Exams/GetExamById/ExamDetailDto.cs`, `GetExamByIdQueryHandler.cs`
- Modify: `src/ExamSystem.Application/Features/Exams/CloneExam/CloneExamCommandHandler.cs`
- Modify: `src/ExamSystem.Api/Controllers/ExamsController.cs` (`UpdateExamRequest`)
- Modify: `frontend/src/app/core/services/exam.service.ts`, `frontend/src/app/features/admin/exams/exam-form.component.ts`, `exam-form.component.html`

- [ ] **Step 1: Add the fields to Create/Update commands + handlers + validator**

`CreateExamCommand.cs`: add two parameters before `List<ExamTopicSelectionInput> TopicSelections`:

```csharp
    int MaxConcurrentAttempts,
    int GraceWindowMinutes,
```

`CreateExamCommandHandler.cs`: in the `new Exam { ... }` initializer, add:

```csharp
            MaxConcurrentAttempts = request.MaxConcurrentAttempts,
            GraceWindowMinutes = request.GraceWindowMinutes,
```

`CreateExamCommandValidator.cs`: add:

```csharp
        RuleFor(x => x.MaxConcurrentAttempts).GreaterThanOrEqualTo(1).WithMessage("Max concurrent attempts must be at least 1.");
        RuleFor(x => x.GraceWindowMinutes).GreaterThanOrEqualTo(1).WithMessage("Grace window must be at least 1 minute.");
```

`UpdateExamCommand.cs`: add the same two parameters before `List<ExamTopicSelectionInput> TopicSelections`.

`UpdateExamCommandHandler.cs`: where it assigns exam fields from the request, add:

```csharp
        exam.MaxConcurrentAttempts = request.MaxConcurrentAttempts;
        exam.GraceWindowMinutes = request.GraceWindowMinutes;
```

- [ ] **Step 2: Add the fields to the read DTO + clone**

`ExamDetailDto.cs`: add `int MaxConcurrentAttempts, int GraceWindowMinutes,` after `int MaxAttempts,`.

`GetExamByIdQueryHandler.cs`: where it constructs the `ExamDetailDto`, pass `exam.MaxConcurrentAttempts, exam.GraceWindowMinutes` in the same positions.

`CloneExamCommandHandler.cs`: where it copies exam fields into the clone, add:

```csharp
            MaxConcurrentAttempts = source.MaxConcurrentAttempts,
            GraceWindowMinutes = source.GraceWindowMinutes,
```

`ExamsController.cs`: add `int MaxConcurrentAttempts, int GraceWindowMinutes,` to the `UpdateExamRequest` record (after `int MaxAttempts`) and pass them into the `new UpdateExamCommand(...)` in the same positions.

- [ ] **Step 3: Update the create/update integration payloads that omit the fields**

The existing `ExamsControllerTests` build exam payloads without the new fields. C# maps missing JSON to `0`,
which fails the `>= 1` validators. In `tests/ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs`,
add `maxConcurrentAttempts = 20, graceWindowMinutes = 3,` to `BuildExamPayload`'s anonymous object (after
`maxAttempts = 1,`). Do the same for any other exam payload in that file.

- [ ] **Step 4: Build + run the affected backend tests**

Run: `dotnet build ExamSystem.sln`
Expected: Build succeeded.
Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter FullyQualifiedName~ExamsControllerTests`
Expected: PASS.

- [ ] **Step 5: Add the fields to the Angular exam service + form model**

`frontend/src/app/core/services/exam.service.ts`: add `maxConcurrentAttempts: number;` and
`graceWindowMinutes: number;` to both the `ExamInput` and `ExamDetail`/`ExamSummary`-adjacent `ExamDetail`
interface (the one extending `ExamInput` already inherits them — add only to `ExamInput`).

`exam-form.component.ts`:
- In `DEFAULT_FORM_VALUES`, add `maxConcurrentAttempts: 20,` and `graceWindowMinutes: 3,`.
- In `rebuildForm()`'s `patchValue({...})` for `initialValue`, add
  `maxConcurrentAttempts: this.initialValue.maxConcurrentAttempts, graceWindowMinutes: this.initialValue.graceWindowMinutes,`.
- In `submit()`'s `this.save.emit({...})`, add
  `maxConcurrentAttempts: Number(value.maxConcurrentAttempts), graceWindowMinutes: Number(value.graceWindowMinutes),`.

- [ ] **Step 6: Add the inputs to the exam form template**

`exam-form.component.html`: add a field row alongside the other numeric config rows (e.g. after the
`passMarkPercentage` / `maxAttempts` row):

```html
  <div class="field-row">
    <div class="field">
      <label for="maxConcurrentAttempts">أقصى عدد متزامن</label>
      <input id="maxConcurrentAttempts" type="number" min="1" formControlName="maxConcurrentAttempts" />
    </div>
    <div class="field">
      <label for="graceWindowMinutes">مهلة الاستدعاء (دقيقة)</label>
      <input id="graceWindowMinutes" type="number" min="1" formControlName="graceWindowMinutes" />
    </div>
  </div>
```

- [ ] **Step 7: Build the frontend**

Run: `cd frontend && npm run build`
Expected: Build succeeds.

- [ ] **Step 8: Commit**

```bash
git add src/ExamSystem.Application/Features/Exams/ src/ExamSystem.Api/Controllers/ExamsController.cs tests/ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs frontend/src/app/core/services/exam.service.ts frontend/src/app/features/admin/exams/exam-form.component.ts frontend/src/app/features/admin/exams/exam-form.component.html
git commit -m "feat: admin-configurable MaxConcurrentAttempts and GraceWindowMinutes"
```

---

## Task 7: Frontend — start branching + waiting room

**Files:**
- Modify: `frontend/src/app/core/services/candidate-exam.service.ts`
- Modify: `frontend/src/app/features/candidate/instructions.component.ts`
- Create: `frontend/src/app/features/candidate/waiting-room.component.ts` (+ `.html`, `.spec.ts`)
- Modify: `frontend/src/app/features/candidate/candidate.routes.ts`
- Modify: `frontend/src/styles/_candidate.scss`

- [ ] **Step 1: Widen the service `start` type + add `queueStatus`**

In `frontend/src/app/core/services/candidate-exam.service.ts`, replace the `StartAttemptResult` interface and
add queue types + method:

```typescript
export interface StartAttemptResult {
  outcome: 'Started' | 'Queued';
  attemptId: string | null;
  attemptToken: string | null;
  expiresAtUtc: string | null;
  queuePosition: number | null;
}

export interface QueueStatus {
  status: 'Waiting' | 'Called' | 'Started' | 'Expired' | 'Cancelled' | 'NotQueued';
  position: number;
  estimatedWaitSeconds: number;
}
```

Add inside the class:

```typescript
  queueStatus(examId: string, nationalId: string): Observable<QueueStatus> {
    return this.http.get<QueueStatus>(`${this.baseUrl}/${examId}/queue/status`, { params: { nationalId } });
  }
```

- [ ] **Step 2: Branch on outcome in the instructions component**

In `frontend/src/app/features/candidate/instructions.component.ts`, change the `start()` success handler:

```typescript
  start(): void {
    if (this.starting || !this.identity) { return; }
    this.starting = true;
    this.error = null;
    this.service.start(this.examId, this.identity).subscribe({
      next: res => {
        if (res.outcome === 'Started' && res.attemptToken) {
          this.tokenStore.set(this.examId, res.attemptToken);
          this.router.navigate(['attempt'], { relativeTo: this.route.parent });
        } else {
          // Queued: persist identity for polling, then show the waiting room.
          localStorage.setItem('queue_' + this.examId, JSON.stringify(this.identity));
          this.router.navigate(['waiting'], { relativeTo: this.route.parent });
        }
      },
      error: () => { this.starting = false; this.error = 'تعذّر بدء الامتحان — حاول مرة أخرى.'; }
    });
  }
```

- [ ] **Step 3: Write the waiting room component**

Create `frontend/src/app/features/candidate/waiting-room.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CandidateExamService, CandidateIdentity } from '../../core/services/candidate-exam.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';

const POLL_MS = 20000;

@Component({
  selector: 'app-waiting-room',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './waiting-room.component.html'
})
export class WaitingRoomComponent implements OnInit, OnDestroy {
  examId = '';
  position = 0;
  estimatedMinutes = 0;
  starting = false;
  private identity: CandidateIdentity | null = null;
  private timer?: ReturnType<typeof setInterval>;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: CandidateExamService,
    private readonly tokenStore: AttemptTokenStore
  ) {}

  ngOnInit(): void {
    this.examId = this.route.parent?.snapshot.paramMap.get('examId') ?? '';
    const raw = localStorage.getItem('queue_' + this.examId);
    this.identity = raw ? (JSON.parse(raw) as CandidateIdentity) : null;
    if (!this.identity) {
      this.router.navigate(['../'], { relativeTo: this.route });
      return;
    }
    this.poll();
    this.timer = setInterval(() => this.poll(), POLL_MS);
  }

  ngOnDestroy(): void {
    if (this.timer) { clearInterval(this.timer); }
  }

  private poll(): void {
    if (!this.identity) { return; }
    this.service.queueStatus(this.examId, this.identity.nationalId).subscribe({
      next: s => {
        this.position = s.position;
        this.estimatedMinutes = Math.ceil(s.estimatedWaitSeconds / 60);
        if (s.status === 'Called' || s.status === 'Expired') { this.tryStart(); }
      }
    });
  }

  private tryStart(): void {
    if (this.starting || !this.identity) { return; }
    this.starting = true;
    this.service.start(this.examId, this.identity).subscribe({
      next: res => {
        if (res.outcome === 'Started' && res.attemptToken) {
          if (this.timer) { clearInterval(this.timer); }
          localStorage.removeItem('queue_' + this.examId);
          this.tokenStore.set(this.examId, res.attemptToken);
          this.router.navigate(['attempt'], { relativeTo: this.route.parent });
        } else {
          this.position = res.queuePosition ?? this.position;
          this.starting = false; // re-queued; keep polling
        }
      },
      error: () => { this.starting = false; }
    });
  }
}
```

- [ ] **Step 4: Write the waiting room template**

Create `frontend/src/app/features/candidate/waiting-room.component.html`:

```html
<div class="candidate-card">
  <div class="candidate-header">
    <img src="assets/moj-logo.png" alt="شعار وزارة العدل" />
    <div class="exam-title">غرفة الانتظار</div>
  </div>
  <div class="candidate-state">
    <p class="waiting-position">ترتيبك في الطابور: <strong>{{ position }}</strong></p>
    <p class="muted" *ngIf="estimatedMinutes > 0">الوقت التقريبي للانتظار: {{ estimatedMinutes }} دقيقة</p>
    <p class="muted">سيبدأ امتحانك تلقائياً فور توفّر مقعد. أبقِ هذه الصفحة مفتوحة.</p>
    <p class="muted" *ngIf="starting">جارٍ بدء امتحانك…</p>
  </div>
</div>
```

- [ ] **Step 5: Add the waiting route + a small style**

`candidate.routes.ts`: add a child route after `instructions`:

```typescript
      {
        path: 'waiting',
        loadComponent: () => import('./waiting-room.component').then(m => m.WaitingRoomComponent)
      },
```

Append to `frontend/src/styles/_candidate.scss`:

```scss
.waiting-position { font-size: var(--fs-headline); font-weight: var(--fw-bold); }
```

- [ ] **Step 6: Write the waiting room spec**

Create `frontend/src/app/features/candidate/waiting-room.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { WaitingRoomComponent } from './waiting-room.component';
import { CandidateExamService } from '../../core/services/candidate-exam.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';

describe('WaitingRoomComponent', () => {
  let fixture: ComponentFixture<WaitingRoomComponent>;
  let component: WaitingRoomComponent;
  const router = { navigate: jasmine.createSpy('navigate') };
  const service = {
    queueStatus: jasmine.createSpy('queueStatus').and.returnValue(of({ status: 'Waiting', position: 2, estimatedWaitSeconds: 120 })),
    start: jasmine.createSpy('start')
  };
  const tokenStore = { set: () => {}, get: () => null, clear: () => {} };

  beforeEach(async () => {
    localStorage.setItem('queue_e1', JSON.stringify({ fullName: 'a b c d', nationalId: '29912310123404', mobileNumber: '01012345678' }));
    await TestBed.configureTestingModule({
      imports: [WaitingRoomComponent],
      providers: [
        { provide: CandidateExamService, useValue: service },
        { provide: AttemptTokenStore, useValue: tokenStore },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { parent: { snapshot: { paramMap: new Map([['examId', 'e1']]) } } } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(WaitingRoomComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => { component.ngOnDestroy(); localStorage.removeItem('queue_e1'); });

  it('polls queue status and shows the position', () => {
    expect(service.queueStatus).toHaveBeenCalledWith('e1', '29912310123404');
    expect(component.position).toBe(2);
  });
});
```

- [ ] **Step 7: Build + run the full frontend suite**

Run: `cd frontend && npm run build`
Expected: Build succeeds.
Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: PASS (existing + waiting-room spec).

- [ ] **Step 8: Commit**

```bash
git add frontend/src/app/core/services/candidate-exam.service.ts frontend/src/app/features/candidate/ frontend/src/styles/_candidate.scss
git commit -m "feat(candidate-ui): waiting room with polling; start branches to queue"
```

---

## Task 8: End-to-end verification

**Files:** none (verification only)

- [ ] **Step 1: Run both servers** (API applies the `AddWaitingQueueAndCapacity` migration on startup).

- [ ] **Step 2: Create a capped exam.** As admin, create + publish an open exam with `MaxConcurrentAttempts = 1`
  and a small MCQ bank (reuse the seed approach; the admin form now exposes "أقصى عدد متزامن" = 1).

- [ ] **Step 3: Drive two candidates.**
  - Candidate A: `/exam/{examId}` → register → instructions → "ابدأ الامتحان" → lands in the **player**.
  - Candidate B (second browser context / incognito, or clear the candidate's localStorage): same flow →
    "ابدأ الامتحان" → lands in the **waiting room** showing "ترتيبك في الطابور: 1". Confirm via `preview_network`
    that `start` returned `outcome: "Queued"` and `queue/status` returns `Waiting`.
  - Submit A's exam (from A's player). Within ~20s, B's waiting room auto-starts → lands in the player. Confirm
    `queue/status` returned `Called` then `start` returned `Started`.

- [ ] **Step 4: Screenshot** the waiting room and confirm no `preview_console_logs` errors.

- [ ] **Step 5: Final full test run.**
  Run: `dotnet test ExamSystem.sln` and `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
  Expected: all green.

---

## Self-Review — Spec Coverage

- Spec §2 capacity fields (admin-set) → Tasks 1, 6. ✅
- Spec §3 core model (active count, reserved, available, reconciler steps) → Tasks 3 (reconciler) + 4 (gate). ✅
- Spec §4.1 Domain (`Exam` fields, `WaitingQueueEntry`) → Task 1. ✅
- Spec §4.2 Application (`IQueueReconciler`, `StartAttemptCommand` gate, widened DTO, `GetQueueStatusQuery`, admin create/update/DTO) → Tasks 3, 4, 5, 6. ✅
- Spec §4.3 Infrastructure (config + migration + reconciler DI) → Tasks 2, 3. ✅
- Spec §4.4 API (`CandidateQueueController`, start unchanged path) → Task 5. ✅
- Spec §4.5 grace-expiry (Expired → re-enqueue at tail) → Task 3 (expire) + Task 4 (fresh Waiting on next start). ✅
- Spec §5 Frontend (start branch, waiting room, identity persistence, service) → Task 7. ✅
- Spec §6 error/edge (disconnect, grace, idempotency, already-taken/not-open, under-capacity no-regress) → Tasks 4, 5, 7. ✅
- Spec §7 testing (reconciler, start queueing, queue status, integration two-candidate, frontend) → every backend task + Tasks 5, 7. ✅

No placeholders remain. Type/name usage is consistent (`WaitingQueueStatus`, `QueueCapacity`, `IQueueReconciler.ReconcileAsync`, `StartAttemptDto.Started/Queued`, `QueueStatusDto`, service `queueStatus`). **Deferred (per spec §8):** manual mode / `QueueMode`, admin live-counts, rate limiting.
