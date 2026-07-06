# Manual Queue Mode — "Open Next Batch" (FR-8.7) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Per-exam Manual queue mode where no candidate enters without an explicit admin release via a count-controlled «افتح الدفعة التالية» button; mode is live-togglable on Published exams (FR-8.7).

**Architecture:** `QueueMode` enum on `Exam` (default Auto). `QueueReconciler` keeps grace-expiry and position-recompute in both modes but auto-promotes only in Auto; a new `CallNextBatchAsync` primitive does capped manual promotion. `StartAttemptCommandHandler`'s capacity bypass is gated to Auto. Two new CQRS commands (`SetExamQueueMode`, `OpenNextBatch`) exposed as `ExamsController` actions. Admin UI: form select + per-row mode toggle and batch controls.

**Tech Stack:** .NET 8, EF Core 8 (SQL Server), MediatR, FluentValidation, xUnit + EF InMemory, Angular 17 standalone + signals.

**Spec:** `docs/superpowers/specs/2026-07-07-manual-queue-mode-design.md` (approved).

---

## Prerequisites

- [ ] **Step 1: Baseline green + no locking dev server**

Stop any running `dotnet run` dev server (locks build output), then:
Run: `dotnet build` and `dotnet test` from `D:/os/ExamSystem`
Expected: `Build succeeded.`, all tests pass (162 backend as of FR-8.8).

---

## File Structure (target state)

```
src/ExamSystem.Domain/Queue/QueueMode.cs                       (Create)
src/ExamSystem.Domain/Exams/Exam.cs                            (Modify: + QueueMode)
src/ExamSystem.Infrastructure/Migrations/*AddExamQueueMode*    (Generate)
src/ExamSystem.Application/Common/Interfaces/IQueueReconciler.cs (Modify: + CallNextBatchAsync)
src/ExamSystem.Infrastructure/Queue/QueueReconciler.cs         (Modify: mode-aware + shared core)
src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommandHandler.cs (Modify: gate)
src/ExamSystem.Application/Features/Exams/SetQueueMode/{SetExamQueueModeCommand,SetExamQueueModeCommandHandler}.cs (Create)
src/ExamSystem.Application/Features/Exams/OpenNextBatch/{OpenNextBatchCommand,OpenNextBatchCommandValidator,OpenBatchResultDto,OpenNextBatchCommandHandler}.cs (Create)
src/ExamSystem.Application/Features/Exams/{CreateExam,UpdateExam}/*Command.cs + handlers (Modify: + QueueMode)
src/ExamSystem.Application/Features/Exams/CloneExam/CloneExamCommandHandler.cs (Modify: copy QueueMode)
src/ExamSystem.Application/Features/Exams/GetExams/{ExamSummaryDto,GetExamsQueryHandler}.cs (Modify)
src/ExamSystem.Application/Features/Exams/GetExamById/{ExamDetailDto,GetExamByIdQueryHandler}.cs (Modify)
src/ExamSystem.Api/Controllers/ExamsController.cs              (Modify: 2 actions + request records)
tests/ExamSystem.Application.UnitTests/Queue/QueueReconcilerTests.cs (Modify: + manual-mode & batch tests)
tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptQueueingTests.cs (Modify: + 2 tests)
tests/ExamSystem.Application.UnitTests/Features/Exams/SetExamQueueModeCommandHandlerTests.cs (Create)
tests/ExamSystem.Application.UnitTests/Features/Exams/OpenNextBatchCommandHandlerTests.cs (Create)
tests/ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs (Modify: + queue tests)
frontend/src/app/core/services/exam.service.ts                 (Modify)
frontend/src/app/features/admin/exams/exam-form.component.{ts,html} (Modify: queue-mode select)
frontend/src/app/features/admin/exams/exams-list.component.{ts,html} (Modify: toggle + batch controls)
frontend/src/styles/_surfaces.scss                             (Modify: .batch-count-input)
```

---

### Task 1: Domain — `QueueMode` enum + `Exam.QueueMode` + migration

**Files:**
- Create: `src/ExamSystem.Domain/Queue/QueueMode.cs`
- Modify: `src/ExamSystem.Domain/Exams/Exam.cs`
- Generate: migration `AddExamQueueMode`

- [ ] **Step 1: Write `QueueMode`**

`src/ExamSystem.Domain/Queue/QueueMode.cs`:
```csharp
namespace ExamSystem.Domain.Queue;

/// <summary>Batch-gate admission policy for an exam (FR-8.7).</summary>
public enum QueueMode
{
    /// <summary>Slots are released automatically; the reconciler promotes the queue (default).</summary>
    Auto = 0,

    /// <summary>No candidate enters without an explicit admin "open next batch" release.</summary>
    Manual = 1
}
```

- [ ] **Step 2: Add the property to `Exam`**

In `src/ExamSystem.Domain/Exams/Exam.cs`, directly after the `GraceWindowMinutes` property add:
```csharp
    /// <summary>Admission policy (FR-8.7): Auto promotes the queue automatically; Manual only via the admin batch button.</summary>
    public QueueMode QueueMode { get; set; } = QueueMode.Auto;
```
`ExamSystem.Domain.Queue` is already in the Domain `GlobalUsings.cs` (used by `WaitingQueueStatus` consumers); if the compiler disagrees, add `global using ExamSystem.Domain.Queue;` there.

- [ ] **Step 3: Build, then generate + apply the migration**

Run:
```bash
dotnet build src/ExamSystem.Domain/ExamSystem.Domain.csproj
dotnet ef migrations add AddExamQueueMode --project src/ExamSystem.Infrastructure --startup-project src/ExamSystem.Api
dotnet ef database update --project src/ExamSystem.Infrastructure --startup-project src/ExamSystem.Api
```
Expected: `Build succeeded.`, migration adds int column `QueueMode` to `Exams` with default 0 (0 = Auto is correct for all existing rows — no backfill).

- [ ] **Step 4: Commit**

```bash
git add src/ExamSystem.Domain src/ExamSystem.Infrastructure
git commit -m "feat(domain): add QueueMode (Auto/Manual) to Exam with migration (FR-8.7)"
```

---

### Task 2: QueueReconciler — mode-aware reconcile + `CallNextBatchAsync` (TDD)

**Files:**
- Modify: `src/ExamSystem.Application/Common/Interfaces/IQueueReconciler.cs`
- Modify: `src/ExamSystem.Infrastructure/Queue/QueueReconciler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Queue/QueueReconcilerTests.cs`

- [ ] **Step 1: Add the failing tests**

Append inside the existing `QueueReconcilerTests` class (its helpers `Exam(max, grace)`, `InProgress(...)`, `Waiting(...)` already exist; extend the `Exam` helper first by replacing it with):

```csharp
    private static Exam Exam(int max, int grace = 3, QueueMode mode = QueueMode.Auto) =>
        new() { Name = "E", DurationMinutes = 60, MaxConcurrentAttempts = max, GraceWindowMinutes = grace, QueueMode = mode };
```

then add these tests:

```csharp
    [Fact]
    public async Task Reconcile_ManualMode_DoesNotPromote_ButStillExpiresAndRepositions()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 5, mode: QueueMode.Manual);
        db.Exams.Add(exam);
        var stale = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-10));
        stale.Status = WaitingQueueStatus.Called;
        stale.CalledAtUtc = DateTime.UtcNow.AddMinutes(-5); // past 3-min grace
        var first = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-4));
        var second = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-2));
        db.WaitingQueueEntries.AddRange(stale, second, first);
        await db.SaveChangesAsync(CancellationToken.None);

        var capacity = await new QueueReconciler(db).ReconcileAsync(exam.Id, CancellationToken.None);

        // grace expiry still happens
        Assert.Equal(WaitingQueueStatus.Expired, db.WaitingQueueEntries.Single(e => e.Id == stale.Id).Status);
        // but nobody is promoted despite 5 free slots
        Assert.Equal(0, db.WaitingQueueEntries.Count(e => e.Status == WaitingQueueStatus.Called));
        // positions still honest
        Assert.Equal(1, db.WaitingQueueEntries.Single(e => e.Id == first.Id).Position);
        Assert.Equal(2, db.WaitingQueueEntries.Single(e => e.Id == second.Id).Position);
        Assert.Equal(5, capacity.Available);
    }

    [Fact]
    public async Task CallNextBatch_PromotesEarliest_CappedByCountAvailableAndWaiting()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 3, mode: QueueMode.Manual);
        db.Exams.Add(exam);
        db.ExamAttempts.Add(InProgress(exam.Id, DateTime.UtcNow.AddMinutes(30))); // 1 slot busy -> available = 2
        var first = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-9));
        var second = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-6));
        var third = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-3));
        db.WaitingQueueEntries.AddRange(third, first, second); // out of order on purpose
        await db.SaveChangesAsync(CancellationToken.None);

        var calledCount = await new QueueReconciler(db).CallNextBatchAsync(exam.Id, 10, CancellationToken.None);

        Assert.Equal(2, calledCount); // requested 10, capped by available (2)
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == first.Id).Status);
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == second.Id).Status);
        Assert.Equal(WaitingQueueStatus.Waiting, db.WaitingQueueEntries.Single(e => e.Id == third.Id).Status);
        Assert.Equal(1, db.WaitingQueueEntries.Single(e => e.Id == third.Id).Position); // repositioned
    }

    [Fact]
    public async Task CallNextBatch_ExpiresStaleGraceFirst_FreeingTheSlot()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 1, grace: 3, mode: QueueMode.Manual);
        db.Exams.Add(exam);
        var stale = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-10));
        stale.Status = WaitingQueueStatus.Called;
        stale.CalledAtUtc = DateTime.UtcNow.AddMinutes(-5); // past grace -> its slot must free up
        var next = Waiting(exam.Id, DateTime.UtcNow.AddMinutes(-4));
        db.WaitingQueueEntries.AddRange(stale, next);
        await db.SaveChangesAsync(CancellationToken.None);

        var calledCount = await new QueueReconciler(db).CallNextBatchAsync(exam.Id, 1, CancellationToken.None);

        Assert.Equal(1, calledCount);
        Assert.Equal(WaitingQueueStatus.Expired, db.WaitingQueueEntries.Single(e => e.Id == stale.Id).Status);
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == next.Id).Status);
    }

    [Fact]
    public async Task CallNextBatch_EmptyQueue_ReturnsZero()
    {
        using var db = TestDbContextFactory.Create();
        var exam = Exam(max: 5, mode: QueueMode.Manual);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var calledCount = await new QueueReconciler(db).CallNextBatchAsync(exam.Id, 3, CancellationToken.None);

        Assert.Equal(0, calledCount);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter QueueReconcilerTests`
Expected: FAIL to compile — `QueueMode` ctor arg on the helper is fine (Task 1 shipped it), but `CallNextBatchAsync` does not exist.

- [ ] **Step 3: Extend the interface**

Replace `src/ExamSystem.Application/Common/Interfaces/IQueueReconciler.cs` with:
```csharp
namespace ExamSystem.Application.Common.Interfaces;

/// <summary>
/// Lazy batch-gate reconciliation (FR-8): expire grace-timed-out Called reservations, promote the
/// earliest Waiting candidates while capacity allows (Auto mode only — Manual exams promote solely
/// via <see cref="CallNextBatchAsync"/>), and recompute Waiting positions. Runs on every
/// start / queue-status request; returns the post-reconciliation capacity. Persists its changes.
/// </summary>
public interface IQueueReconciler
{
    Task<QueueCapacity> ReconcileAsync(Guid examId, CancellationToken cancellationToken);

    /// <summary>
    /// Manual promotion primitive (FR-8.7): expire stale grace reservations, then promote the earliest
    /// Waiting entries capped by min(maxToCall, available capacity, waiting count). Returns how many
    /// were promoted to Called.
    /// </summary>
    Task<int> CallNextBatchAsync(Guid examId, int maxToCall, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Rewrite `QueueReconciler` with a shared core**

Replace `src/ExamSystem.Infrastructure/Queue/QueueReconciler.cs` with:
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
        var (capacity, _) = await ReconcileCoreAsync(examId, promoteLimit: null, cancellationToken);
        return capacity;
    }

    public async Task<int> CallNextBatchAsync(Guid examId, int maxToCall, CancellationToken cancellationToken)
    {
        var (_, called) = await ReconcileCoreAsync(examId, promoteLimit: Math.Max(0, maxToCall), cancellationToken);
        return called;
    }

    /// <summary>
    /// Shared reconciliation core. promoteLimit == null -> mode-driven promotion (Auto: unbounded,
    /// Manual: none). promoteLimit == n -> promote up to n (the manual batch button, FR-8.7).
    /// Capacity is always the hard cap.
    /// </summary>
    private async Task<(QueueCapacity Capacity, int Called)> ReconcileCoreAsync(
        Guid examId, int? promoteLimit, CancellationToken cancellationToken)
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

        // 2. Promote earliest Waiting while capacity allows, bounded by the promotion limit.
        var waiting = await _db.WaitingQueueEntries
            .Where(e => e.ExamId == examId && e.Status == WaitingQueueStatus.Waiting)
            .OrderBy(e => e.EnqueuedAtUtc)
            .ToListAsync(cancellationToken);

        var available = Math.Max(0, exam.MaxConcurrentAttempts - activeAttempts - reserved);
        var limit = promoteLimit ?? (exam.QueueMode == QueueMode.Auto ? int.MaxValue : 0);
        var toPromote = Math.Min(limit, Math.Min(available, waiting.Count));

        var index = 0;
        for (; index < toPromote; index++, available--, reserved++)
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
        return (new QueueCapacity(exam.MaxConcurrentAttempts, activeAttempts, reserved), toPromote);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter QueueReconcilerTests`
Expected: `Passed!` — all pre-existing reconciler tests (Auto behavior unchanged) plus the 4 new ones.

- [ ] **Step 6: Run the full unit suite (regression)**

Run: `dotnet test tests/ExamSystem.Application.UnitTests`
Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src/ExamSystem.Application src/ExamSystem.Infrastructure tests/ExamSystem.Application.UnitTests
git commit -m "feat(queue): mode-aware reconciliation + CallNextBatchAsync primitive (FR-8.7)"
```

---

### Task 3: StartAttempt — Manual-mode entry gate (TDD)

**Files:**
- Modify: `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/CandidateExam/StartAttemptQueueingTests.cs`

- [ ] **Step 1: Add the failing tests**

Append inside `StartAttemptQueueingTests` (helpers `SeedAsync`, `Handler`, `Nid`, `Nid2` already exist — note `SeedAsync` builds an Auto exam; set the mode explicitly in the new tests):

```csharp
    [Fact]
    public async Task Start_ManualMode_WithFreeCapacity_StillQueues()
    {
        var (db, exam) = await SeedAsync(max: 20);
        exam.QueueMode = QueueMode.Manual;
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.Equal("Queued", result.Value!.Outcome);
        Assert.Equal(1, result.Value.QueuePosition);
        Assert.Empty(db.ExamAttempts);
    }

    [Fact]
    public async Task Start_ManualMode_CalledCandidate_Starts()
    {
        var (db, exam) = await SeedAsync(max: 20);
        exam.QueueMode = QueueMode.Manual;
        await db.SaveChangesAsync(CancellationToken.None);

        // enqueue, then the admin opens a batch of 1
        await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);
        await new QueueReconciler(db).CallNextBatchAsync(exam.Id, 1, CancellationToken.None);

        var retry = await Handler(db).Handle(new StartAttemptCommand(exam.Id, "احمد محمد علي حسن", Nid, "01012345678"), CancellationToken.None);

        Assert.Equal("Started", retry.Value!.Outcome);
        Assert.Equal(WaitingQueueStatus.Started, db.WaitingQueueEntries.Single().Status);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter StartAttemptQueueingTests`
Expected: `Start_ManualMode_WithFreeCapacity_StillQueues` FAILS (outcome is "Started" — the capacity bypass ignores mode). The Called test may already pass; that's fine.

- [ ] **Step 3: Gate the capacity bypass by mode**

In `src/ExamSystem.Application/Features/CandidateExam/StartAttempt/StartAttemptCommandHandler.cs`, replace:
```csharp
        if (called is not null || capacity.Available > 0)
```
with:
```csharp
        // FR-8.7: in Manual mode only admin-Called candidates may enter; free capacity alone is not a ticket.
        if (called is not null || (exam.QueueMode == QueueMode.Auto && capacity.Available > 0))
```
(`ExamSystem.Domain.Queue` is already imported at the top of the file.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter StartAttempt`
Expected: all StartAttempt tests pass (existing Auto tests + 2 new Manual tests).

- [ ] **Step 5: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(candidate): gate exam entry by QueueMode — Manual admits only Called (FR-8.7)"
```

---

### Task 4: Application — SetExamQueueMode + OpenNextBatch commands (TDD)

**Files:**
- Create: `src/ExamSystem.Application/Features/Exams/SetQueueMode/SetExamQueueModeCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/SetQueueMode/SetExamQueueModeCommandHandler.cs`
- Create: `src/ExamSystem.Application/Features/Exams/OpenNextBatch/OpenNextBatchCommand.cs`
- Create: `src/ExamSystem.Application/Features/Exams/OpenNextBatch/OpenNextBatchCommandValidator.cs`
- Create: `src/ExamSystem.Application/Features/Exams/OpenNextBatch/OpenBatchResultDto.cs`
- Create: `src/ExamSystem.Application/Features/Exams/OpenNextBatch/OpenNextBatchCommandHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/SetExamQueueModeCommandHandlerTests.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/OpenNextBatchCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing tests for `SetExamQueueModeCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/SetExamQueueModeCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.SetQueueMode;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class SetExamQueueModeCommandHandlerTests
{
    private static Exam NewExam(ExamStatus status) => new()
    {
        Name = "E",
        StartAtUtc = DateTime.UtcNow.AddHours(-1),
        EndAtUtc = DateTime.UtcNow.AddDays(1),
        DurationMinutes = 60,
        Status = status
    };

    [Theory]
    [InlineData(ExamStatus.Draft)]
    [InlineData(ExamStatus.Published)]
    public async Task Handle_DraftOrPublished_SetsMode(ExamStatus status)
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(status);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await new SetExamQueueModeCommandHandler(db)
            .Handle(new SetExamQueueModeCommand(exam.Id, QueueMode.Manual), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(QueueMode.Manual, db.Exams.Single().QueueMode);
    }

    [Theory]
    [InlineData(ExamStatus.Closed)]
    [InlineData(ExamStatus.Archived)]
    public async Task Handle_ClosedOrArchived_Fails(ExamStatus status)
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(status);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await new SetExamQueueModeCommandHandler(db)
            .Handle(new SetExamQueueModeCommand(exam.Id, QueueMode.Manual), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(QueueMode.Auto, db.Exams.Single().QueueMode);
    }

    [Fact]
    public async Task Handle_SameMode_IsIdempotentSuccess()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(ExamStatus.Published);
        exam.QueueMode = QueueMode.Manual;
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await new SetExamQueueModeCommandHandler(db)
            .Handle(new SetExamQueueModeCommand(exam.Id, QueueMode.Manual), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_UnknownExam_Fails()
    {
        using var db = TestDbContextFactory.Create();

        var result = await new SetExamQueueModeCommandHandler(db)
            .Handle(new SetExamQueueModeCommand(Guid.NewGuid(), QueueMode.Manual), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
```

- [ ] **Step 2: Write the failing tests for `OpenNextBatchCommandHandler`**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/OpenNextBatchCommandHandlerTests.cs`:
```csharp
using ExamSystem.Application.Features.Exams.OpenNextBatch;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;
using ExamSystem.Infrastructure.Queue;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class OpenNextBatchCommandHandlerTests
{
    private static Exam NewExam(ExamStatus status, QueueMode mode, int max = 10) => new()
    {
        Name = "E",
        StartAtUtc = DateTime.UtcNow.AddHours(-1),
        EndAtUtc = DateTime.UtcNow.AddDays(1),
        DurationMinutes = 60,
        MaxConcurrentAttempts = max,
        GraceWindowMinutes = 3,
        Status = status,
        QueueMode = mode
    };

    private static WaitingQueueEntry Waiting(Guid examId, int minutesAgo) => new()
    {
        ExamId = examId, CandidateId = Guid.NewGuid(),
        EnqueuedAtUtc = DateTime.UtcNow.AddMinutes(-minutesAgo),
        Status = WaitingQueueStatus.Waiting
    };

    [Fact]
    public async Task Handle_ManualPublished_CallsBatchAndReportsNumbers()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(ExamStatus.Published, QueueMode.Manual, max: 10);
        db.Exams.Add(exam);
        db.WaitingQueueEntries.AddRange(Waiting(exam.Id, 9), Waiting(exam.Id, 6), Waiting(exam.Id, 3));
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new OpenNextBatchCommand(exam.Id, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.CalledCount);
        Assert.Equal(1, result.Value.RemainingWaiting);
        Assert.Equal(8, result.Value.AvailableAfter); // 10 - 0 active - 2 reserved
    }

    [Fact]
    public async Task Handle_EmptyQueue_SucceedsWithZeroCalled()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(ExamStatus.Published, QueueMode.Manual);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new OpenNextBatchCommand(exam.Id, 5), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.CalledCount);
    }

    [Fact]
    public async Task Handle_AutoModeExam_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(ExamStatus.Published, QueueMode.Auto);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new OpenNextBatchCommand(exam.Id, 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("manual", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ExamStatus.Draft)]
    [InlineData(ExamStatus.Closed)]
    [InlineData(ExamStatus.Archived)]
    public async Task Handle_NotPublished_Fails(ExamStatus status)
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam(status, QueueMode.Manual);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));
        var result = await handler.Handle(new OpenNextBatchCommand(exam.Id, 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_UnknownExam_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var handler = new OpenNextBatchCommandHandler(db, new QueueReconciler(db));

        var result = await handler.Handle(new OpenNextBatchCommand(Guid.NewGuid(), 1), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter "SetExamQueueModeCommandHandlerTests|OpenNextBatchCommandHandlerTests"`
Expected: FAIL to compile — the new types don't exist.

- [ ] **Step 4: Implement the commands**

`src/ExamSystem.Application/Features/Exams/SetQueueMode/SetExamQueueModeCommand.cs`:
```csharp
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.SetQueueMode;

public record SetExamQueueModeCommand(Guid ExamId, QueueMode Mode) : IRequest<Result<Unit>>;
```

`src/ExamSystem.Application/Features/Exams/SetQueueMode/SetExamQueueModeCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Exams;

namespace ExamSystem.Application.Features.Exams.SetQueueMode;

public class SetExamQueueModeCommandHandler : IRequestHandler<SetExamQueueModeCommand, Result<Unit>>
{
    private readonly IApplicationDbContext _db;

    public SetExamQueueModeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(SetExamQueueModeCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<Unit>.Failure("Exam not found.");
        }

        if (exam.Status is not (ExamStatus.Draft or ExamStatus.Published))
        {
            return Result<Unit>.Failure("Queue mode can only be changed for draft or published exams.");
        }

        exam.QueueMode = request.Mode;
        exam.ModifiedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}
```

`src/ExamSystem.Application/Features/Exams/OpenNextBatch/OpenNextBatchCommand.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.OpenNextBatch;

public record OpenNextBatchCommand(Guid ExamId, int Count) : IRequest<Result<OpenBatchResultDto>>;
```

`src/ExamSystem.Application/Features/Exams/OpenNextBatch/OpenNextBatchCommandValidator.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.OpenNextBatch;

public class OpenNextBatchCommandValidator : AbstractValidator<OpenNextBatchCommand>
{
    public OpenNextBatchCommandValidator()
    {
        RuleFor(x => x.Count).GreaterThanOrEqualTo(1).WithMessage("Batch count must be at least 1.");
    }
}
```

`src/ExamSystem.Application/Features/Exams/OpenNextBatch/OpenBatchResultDto.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.OpenNextBatch;

/// <summary>Outcome of an admin "open next batch" action (FR-8.7).</summary>
public record OpenBatchResultDto(int CalledCount, int RemainingWaiting, int AvailableAfter);
```

`src/ExamSystem.Application/Features/Exams/OpenNextBatch/OpenNextBatchCommandHandler.cs`:
```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.OpenNextBatch;

public class OpenNextBatchCommandHandler : IRequestHandler<OpenNextBatchCommand, Result<OpenBatchResultDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IQueueReconciler _reconciler;

    public OpenNextBatchCommandHandler(IApplicationDbContext db, IQueueReconciler reconciler)
    {
        _db = db;
        _reconciler = reconciler;
    }

    public async Task<Result<OpenBatchResultDto>> Handle(OpenNextBatchCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams.FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);
        if (exam is null)
        {
            return Result<OpenBatchResultDto>.Failure("Exam not found.");
        }

        if (exam.Status != ExamStatus.Published)
        {
            return Result<OpenBatchResultDto>.Failure("Exam is not published.");
        }

        if (exam.QueueMode != QueueMode.Manual)
        {
            return Result<OpenBatchResultDto>.Failure("Exam queue is not in manual mode.");
        }

        var calledCount = await _reconciler.CallNextBatchAsync(request.ExamId, request.Count, cancellationToken);

        // Post-call numbers for the admin toast. CallNextBatchAsync just expired stale grace
        // reservations, so every remaining Called entry is within its grace window.
        var now = DateTime.UtcNow;
        var remainingWaiting = await _db.WaitingQueueEntries.CountAsync(
            e => e.ExamId == request.ExamId && e.Status == WaitingQueueStatus.Waiting, cancellationToken);
        var reserved = await _db.WaitingQueueEntries.CountAsync(
            e => e.ExamId == request.ExamId && e.Status == WaitingQueueStatus.Called, cancellationToken);
        var active = await _db.ExamAttempts.CountAsync(
            a => a.ExamId == request.ExamId && a.Status == ExamAttemptStatus.InProgress && a.ExpiresAtUtc > now,
            cancellationToken);
        var availableAfter = Math.Max(0, exam.MaxConcurrentAttempts - active - reserved);

        return Result<OpenBatchResultDto>.Success(new OpenBatchResultDto(calledCount, remainingWaiting, availableAfter));
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter "SetExamQueueModeCommandHandlerTests|OpenNextBatchCommandHandlerTests"`
Expected: `Passed! - Failed: 0, Passed: 10`

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): SetExamQueueMode and OpenNextBatch commands (FR-8.7)"
```

---

### Task 5: Config surface + API endpoints + integration tests

**Files:**
- Modify: `src/ExamSystem.Application/Features/Exams/CreateExam/CreateExamCommand.cs` (+ handler)
- Modify: `src/ExamSystem.Application/Features/Exams/UpdateExam/UpdateExamCommand.cs` (+ handler)
- Modify: `src/ExamSystem.Application/Features/Exams/CloneExam/CloneExamCommandHandler.cs`
- Modify: `src/ExamSystem.Application/Features/Exams/GetExams/ExamSummaryDto.cs` (+ handler)
- Modify: `src/ExamSystem.Application/Features/Exams/GetExamById/ExamDetailDto.cs` (+ handler)
- Modify: `src/ExamSystem.Api/Controllers/ExamsController.cs`
- Test: `tests/ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs`
- Modify (compiler-driven): unit-test files that construct the commands/DTOs positionally

- [ ] **Step 1: Add `QueueMode` to the command records**

In both `CreateExamCommand.cs` and `UpdateExamCommand.cs`, add a parameter directly after `int GraceWindowMinutes,`:
```csharp
    QueueMode QueueMode,
```
and add `using ExamSystem.Domain.Queue;` at the top of each file.

- [ ] **Step 2: Assign it in the handlers**

In `CreateExamCommandHandler.cs`, after `GraceWindowMinutes = request.GraceWindowMinutes,` add:
```csharp
            QueueMode = request.QueueMode,
```
In `UpdateExamCommandHandler.cs`, after `exam.GraceWindowMinutes = request.GraceWindowMinutes;` add:
```csharp
        exam.QueueMode = request.QueueMode;
```
In `CloneExamCommandHandler.cs`, find where the clone copies `GraceWindowMinutes` from the source exam and add the sibling line:
```csharp
            QueueMode = source.QueueMode,
```
(adjust the receiver name to whatever variable that handler uses for the source exam — read the file first).

- [ ] **Step 3: Add `QueueMode` to the read DTOs**

`ExamSummaryDto.cs` — new shape:
```csharp
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.GetExams;

public record ExamSummaryDto(
    Guid Id, string Name, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
    ExamStatus Status, QueueMode QueueMode, int TotalQuestionCount, decimal TotalPoints);
```
In `GetExamsQueryHandler.cs` update the projection to pass `e.QueueMode` between `e.Status` and the question-count sum.

`ExamDetailDto.cs` — new shape:
```csharp
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.GetExamById;

public record ExamDetailDto(
    Guid Id, string Name, string? Description, DateTime StartAtUtc, DateTime EndAtUtc, int DurationMinutes,
    decimal McqPoints, decimal TrueFalsePoints, decimal FillBlankPoints, decimal PassMarkPercentage, int MaxAttempts,
    bool ShuffleAnswers, bool ShowResultImmediately, bool AllowBackNavigation,
    int MaxConcurrentAttempts, int GraceWindowMinutes, QueueMode QueueMode, ExamStatus Status,
    List<ExamTopicSelectionDto> TopicSelections);
```
In `GetExamByIdQueryHandler.cs` update the construction to pass `exam.QueueMode` between `exam.GraceWindowMinutes` and `exam.Status`.

- [ ] **Step 4: Fix the compiler-flagged positional call sites**

Run `dotnet build` — it will flag every positional constructor call missing the new argument. Fix each by inserting the argument in the same position as the record declares it:
- Command constructions in `tests/ExamSystem.Application.UnitTests/Features/Exams/CreateExamCommandValidatorTests.cs`, `CreateExamCommandHandlerTests.cs`, `UpdateExamCommandHandlerTests.cs` (and `CloneExamCommandHandlerTests.cs` if it builds commands): insert `QueueMode.Auto` after the `GraceWindowMinutes` argument (add `using ExamSystem.Domain.Queue;` where missing).
- The `ExamsController.Update` action and its `UpdateExamRequest` record (Step 5 below) — done as part of the controller edit.
Repeat `dotnet build` until clean. Do NOT change any test's assertions — only constructor arguments.

- [ ] **Step 5: Controller — request records + the two new actions**

In `src/ExamSystem.Api/Controllers/ExamsController.cs`:

1. Add usings:
```csharp
using ExamSystem.Application.Features.Exams.OpenNextBatch;
using ExamSystem.Application.Features.Exams.SetQueueMode;
using ExamSystem.Domain.Queue;
```
2. In the `UpdateExamRequest` record, add `QueueMode QueueMode,` after `int GraceWindowMinutes,`; in the `Update` action pass `request.QueueMode` into the `UpdateExamCommand` after `request.GraceWindowMinutes`.
3. Add the two actions after `Clone`:
```csharp
    /// <summary>Switch the batch-gate admission policy (FR-8.7). Allowed for Draft and Published exams.</summary>
    [HttpPost("{id:guid}/queue-mode")]
    public async Task<IActionResult> SetQueueMode(Guid id, [FromBody] SetQueueModeRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetExamQueueModeCommand(id, request.Mode), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return NoContent();
    }

    /// <summary>Admin "open next batch" (FR-8.7): promote up to Count waiting candidates, capped by capacity.</summary>
    [HttpPost("{id:guid}/queue/open-batch")]
    public async Task<IActionResult> OpenBatch(Guid id, [FromBody] OpenBatchRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new OpenNextBatchCommand(id, request.Count), cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { errors = result.Errors });
        }
        return Ok(result.Value);
    }

    public record SetQueueModeRequest(QueueMode Mode);
    public record OpenBatchRequest(int Count);
```

- [ ] **Step 6: Write the failing integration tests**

Append inside `ExamsControllerTests` (helpers `CreateAuthenticatedAdminClientAsync`, `CreateTopicAsync`, `CreateMcqQuestionAsync`, `BuildExamPayload`, `IdResponse` already exist):
```csharp
    private sealed record OpenBatchResponse(int CalledCount, int RemainingWaiting, int AvailableAfter);

    [Fact]
    public async Task QueueEndpoints_Anonymous_ReturnUnauthorized()
    {
        var client = _factory.CreateClient();

        var modeResponse = await client.PostAsJsonAsync($"/api/admin/exams/{Guid.NewGuid()}/queue-mode", new { mode = "Manual" });
        var batchResponse = await client.PostAsJsonAsync($"/api/admin/exams/{Guid.NewGuid()}/queue/open-batch", new { count = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, modeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, batchResponse.StatusCode);
    }

    [Fact]
    public async Task SetManualThenOpenBatch_OnPublishedExam_Succeeds()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - ManualQueue");
        await CreateMcqQuestionAsync(client, topicId, "Medium");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, 1));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();
        (await client.PostAsync($"/api/admin/exams/{created!.Id}/publish", null)).EnsureSuccessStatusCode();

        var modeResponse = await client.PostAsJsonAsync($"/api/admin/exams/{created.Id}/queue-mode", new { mode = "Manual" });
        Assert.Equal(HttpStatusCode.NoContent, modeResponse.StatusCode);

        var batchResponse = await client.PostAsJsonAsync($"/api/admin/exams/{created.Id}/queue/open-batch", new { count = 3 });
        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);
        var body = await batchResponse.Content.ReadFromJsonAsync<OpenBatchResponse>();
        Assert.Equal(0, body!.CalledCount); // queue is empty
        Assert.Equal(20, body.AvailableAfter);
    }

    [Fact]
    public async Task OpenBatch_OnAutoModeExam_ReturnsBadRequest()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - AutoQueueBatch");
        await CreateMcqQuestionAsync(client, topicId, "Medium");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, 1));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();
        (await client.PostAsync($"/api/admin/exams/{created!.Id}/publish", null)).EnsureSuccessStatusCode();

        var batchResponse = await client.PostAsJsonAsync($"/api/admin/exams/{created.Id}/queue/open-batch", new { count = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, batchResponse.StatusCode);
    }
```
Note: `BuildExamPayload` is an anonymous object bound by JSON — it does not need a `queueMode` field (missing enum binds to default `Auto`). Do not add one.

- [ ] **Step 7: Run the integration tests**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter "QueueEndpoints_Anonymous_ReturnUnauthorized|SetManualThenOpenBatch|OpenBatch_OnAutoModeExam"`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 8: Run the full backend suite**

Run: `dotnet test`
Expected: all pass, no regressions.

- [ ] **Step 9: Commit**

```bash
git add src/ExamSystem.Application src/ExamSystem.Api tests
git commit -m "feat(api): queue-mode and open-batch endpoints + QueueMode config surface (FR-8.7)"
```

---

### Task 6: Frontend — form select + list controls

**Files:**
- Modify: `frontend/src/app/core/services/exam.service.ts`
- Modify: `frontend/src/app/features/admin/exams/exam-form.component.ts`
- Modify: `frontend/src/app/features/admin/exams/exam-form.component.html`
- Modify: `frontend/src/app/features/admin/exams/exams-list.component.ts`
- Modify: `frontend/src/app/features/admin/exams/exams-list.component.html`
- Modify: `frontend/src/styles/_surfaces.scss`

- [ ] **Step 1: Service — types + methods**

In `frontend/src/app/core/services/exam.service.ts`:
1. After the `ExamStatus` type alias add:
```typescript
export type QueueMode = 'Auto' | 'Manual';
```
2. In `ExamInput` add (after `graceWindowMinutes: number;`):
```typescript
  queueMode: QueueMode;
```
3. In `ExamSummary` add (after `status: ExamStatus;`):
```typescript
  queueMode: QueueMode;
```
(`ExamDetail extends ExamInput`, so it inherits the field.)
4. After the `ExamLiveCounts` interface add:
```typescript
export interface OpenBatchResult {
  calledCount: number;
  remainingWaiting: number;
  availableAfter: number;
}
```
5. Inside `ExamService`, after `getLiveCounts()` add:
```typescript
  setQueueMode(id: string, mode: QueueMode): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/queue-mode`, { mode });
  }

  openNextBatch(id: string, count: number): Observable<OpenBatchResult> {
    return this.http.post<OpenBatchResult>(`${this.baseUrl}/${id}/queue/open-batch`, { count });
  }
```

- [ ] **Step 2: Exam form — queue-mode select**

In `frontend/src/app/features/admin/exams/exam-form.component.ts` (read the file first; it has a `DEFAULTS` object, a `patchValue` block, and an emit mapping):
1. In the `DEFAULTS` object, after `graceWindowMinutes: 3,` add:
```typescript
  queueMode: 'Auto' as const,
```
2. Where the form is patched from `initialValue` (after `graceWindowMinutes: this.initialValue.graceWindowMinutes,`) add:
```typescript
        queueMode: this.initialValue.queueMode,
```
3. In the save/emit mapping (after `graceWindowMinutes: Number(value.graceWindowMinutes),`) add:
```typescript
      queueMode: value.queueMode,
```
4. If the form group is built with explicit control definitions rather than from `DEFAULTS`, add a `queueMode` control initialized to `'Auto'`.

In `frontend/src/app/features/admin/exams/exam-form.component.html`, inside the `field-row` that holds `maxConcurrentAttempts`/`graceWindowMinutes`, add a third field:
```html
    <div class="field">
      <label for="queueMode">وضع الطابور</label>
      <select id="queueMode" formControlName="queueMode">
        <option value="Auto">تلقائي</option>
        <option value="Manual">يدوي — فتح الدفعات من الأدمن</option>
      </select>
    </div>
```

- [ ] **Step 3: Exams-list — mode chip, toggle, batch controls**

In `frontend/src/app/features/admin/exams/exams-list.component.ts`:
1. Extend the service import line with the new types:
```typescript
import { ExamDetail, ExamInput, ExamLiveCounts, ExamService, ExamSummary, QueueMode } from '../../../core/services/exam.service';
```
2. Add inside the class (near `copiedExamId`):
```typescript
  /** Per-exam inline feedback after an "open batch" action (FR-8.7). */
  batchMessage = signal<{ examId: string; text: string } | null>(null);

  toggleQueueMode(exam: ExamSummary): void {
    this.errorMessage = null;
    const next: QueueMode = exam.queueMode === 'Manual' ? 'Auto' : 'Manual';
    this.examService.setQueueMode(exam.id, next).subscribe({
      next: () => this.load(),
      error: err => (this.errorMessage = (err.error?.errors ?? ['تعذّر تغيير وضع الطابور.']).join(' ، '))
    });
  }

  openBatch(examId: string, rawCount: string): void {
    this.errorMessage = null;
    const count = Math.max(1, Number(rawCount) || 1);
    this.examService.openNextBatch(examId, count).subscribe({
      next: result => {
        this.batchMessage.set({ examId, text: `تم استدعاء ${result.calledCount}` });
        setTimeout(() => this.batchMessage.set(null), 3000);
      },
      error: err => (this.errorMessage = (err.error?.errors ?? ['تعذّر فتح الدفعة.']).join(' ، '))
    });
  }
```

In `frontend/src/app/features/admin/exams/exams-list.component.html`:
1. In the status cell, after the FR-8.8 `badge-live` span, add the mode chip:
```html
          <span class="badge badge-neutral" *ngIf="exam.status === 'Published'">
            {{ exam.queueMode === 'Manual' ? 'طابور يدوي' : 'طابور تلقائي' }}
          </span>
```
2. In the actions cell, after the copy-link button, add:
```html
          <button type="button" class="secondary" *ngIf="exam.status === 'Published'" (click)="toggleQueueMode(exam)">
            {{ exam.queueMode === 'Manual' ? 'تحويل لتلقائي' : 'تحويل ليدوي' }}
          </button>
          <ng-container *ngIf="exam.status === 'Published' && exam.queueMode === 'Manual'">
            <input type="number" min="1" value="1" #batchCount class="batch-count-input" aria-label="عدد الدفعة" />
            <button
              type="button"
              (click)="openBatch(exam.id, batchCount.value)"
              [disabled]="(liveFor(exam.id)?.waitingCount ?? 0) === 0"
            >
              افتح الدفعة التالية
            </button>
            <span class="batch-feedback" *ngIf="batchMessage()?.examId === exam.id">{{ batchMessage()?.text }}</span>
          </ng-container>
```

- [ ] **Step 4: Styles**

In `frontend/src/styles/_surfaces.scss`, after the `.badge-live` block, add:
```scss
.batch-count-input {
  width: 64px;
  text-align: center;
  font-variant-numeric: tabular-nums;
}

.batch-feedback {
  color: var(--confirm-green, #0a7d5a);
  font-size: 0.85em;
}
```

- [ ] **Step 5: Build the frontend**

Run: `cd frontend && npx ng build --configuration development`
Expected: build succeeds with no template/type errors.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/core/services/exam.service.ts frontend/src/app/features/admin/exams frontend/src/styles/_surfaces.scss
git commit -m "feat(admin-ui): queue mode select, live toggle and open-next-batch controls (FR-8.7)"
```

---

### Task 7: Manual verification (controller does this in the main session)

- [ ] **Step 1: Run both servers** (`.claude/launch.json` configs `api` + `frontend`).

- [ ] **Step 2: Verify the flow end-to-end**

1. Admin: open `/admin/exams`, pick a Published exam, click «تحويل ليدوي» → chip flips to «طابور يدوي», batch controls appear (button disabled — queue empty).
2. Candidate: register/start on that exam via `/exam/{id}` with a fresh national ID → lands in the waiting room (Queued) despite free capacity.
3. Admin: FR-8.8 badge shows «طابور 1» within 15s; batch button enables; enter 1 → «افتح الدفعة التالية» → feedback «تم استدعاء 1».
4. Candidate: waiting room polls → auto-starts within ~20s (Called), enters the exam.
5. Admin: click «تحويل لتلقائي» → subsequent candidates start immediately while capacity allows.

- [ ] **Step 3: Full backend suite one last time**

Run: `dotnet test`
Expected: all green.

---

## Spec Coverage Summary

| Spec section | Task |
|---|---|
| §3 Domain (enum, property, migration) | Task 1 |
| §4.1 Mode-aware reconcile | Task 2 |
| §4.2 CallNextBatchAsync | Task 2 |
| §4.3 StartAttempt gate | Task 3 |
| §5.1 OpenNextBatch command (+ validator, DTO) | Task 4 |
| §5.2 SetQueueMode command | Task 4 |
| §5.3 Config surface (commands, DTOs, clone) | Task 5 |
| §6 API endpoints | Task 5 |
| §7 Frontend (form select, chip, toggle, batch button) | Task 6 |
| §8 Testing (reconciler, StartAttempt, commands, integration) | Tasks 2–5 |
| §9 Out of scope | (untouched) |
