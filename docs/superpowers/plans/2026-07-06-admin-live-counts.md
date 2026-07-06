# Admin Live Counts (FR-8.8) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the admin live numbers per Published exam — active candidates / capacity / reserved (called) / queue length — as polling badges inside the existing `/admin/exams` list (FR-8.8).

**Architecture:** One new read-only CQRS query (`GetExamLiveCounts`) that mirrors the `QueueReconciler` count definitions without persisting anything; one new GET action on the existing `ExamsController`; a 15-second RxJS polling loop in `exams-list.component` that renders a live badge on Published rows. No SignalR, no new tables, no migration.

**Tech Stack:** .NET 8, EF Core 8, MediatR, xUnit + EF Core InMemory, Angular 17 standalone + signals + rxjs-interop.

**Spec:** `docs/superpowers/specs/2026-07-06-admin-live-counts-design.md` (approved).

---

## Prerequisites

- [ ] **Step 1: Baseline is green**

Run: `dotnet build` then `dotnet test` from `D:/os/ExamSystem`
Expected: `Build succeeded.`, all tests pass.

---

## File Structure (target state)

```
src/ExamSystem.Application/Features/Exams/GetExamLiveCounts/
├─ ExamLiveCountsDto.cs           (Create)
├─ GetExamLiveCountsQuery.cs      (Create)
└─ GetExamLiveCountsQueryHandler.cs (Create)
src/ExamSystem.Api/Controllers/ExamsController.cs   (Modify: add live-counts action)
tests/ExamSystem.Application.UnitTests/Features/Exams/GetExamLiveCountsQueryHandlerTests.cs (Create)
tests/ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs (Modify: add 2 tests)
frontend/src/app/core/services/exam.service.ts       (Modify: interface + getLiveCounts)
frontend/src/app/features/admin/exams/exams-list.component.ts   (Modify: polling + lookup)
frontend/src/app/features/admin/exams/exams-list.component.html (Modify: live badge)
frontend/src/styles/_surfaces.scss                   (Modify: .badge-live)
```

---

### Task 1: Application — GetExamLiveCounts query (TDD)

**Files:**
- Create: `src/ExamSystem.Application/Features/Exams/GetExamLiveCounts/ExamLiveCountsDto.cs`
- Create: `src/ExamSystem.Application/Features/Exams/GetExamLiveCounts/GetExamLiveCountsQuery.cs`
- Create: `src/ExamSystem.Application/Features/Exams/GetExamLiveCounts/GetExamLiveCountsQueryHandler.cs`
- Test: `tests/ExamSystem.Application.UnitTests/Features/Exams/GetExamLiveCountsQueryHandlerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/ExamSystem.Application.UnitTests/Features/Exams/GetExamLiveCountsQueryHandlerTests.cs`:

```csharp
using ExamSystem.Application.Features.Exams.GetExamLiveCounts;
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.UnitTests.Features.Exams;

public class GetExamLiveCountsQueryHandlerTests
{
    private static Exam NewExam(string name, ExamStatus status) => new()
    {
        Name = name,
        StartAtUtc = DateTime.UtcNow.AddHours(-1),
        EndAtUtc = DateTime.UtcNow.AddDays(1),
        DurationMinutes = 60,
        MaxConcurrentAttempts = 20,
        GraceWindowMinutes = 3,
        Status = status
    };

    private static ExamAttempt NewAttempt(Guid examId, ExamAttemptStatus status, DateTime expiresAtUtc) => new()
    {
        ExamId = examId,
        CandidateId = Guid.NewGuid(),
        StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
        ExpiresAtUtc = expiresAtUtc,
        Status = status
    };

    private static WaitingQueueEntry NewQueueEntry(Guid examId, WaitingQueueStatus status, DateTime? calledAtUtc = null) => new()
    {
        ExamId = examId,
        CandidateId = Guid.NewGuid(),
        EnqueuedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        Position = 0,
        Status = status,
        CalledAtUtc = calledAtUtc
    };

    [Fact]
    public async Task Handle_PublishedExam_ReturnsEffectiveCountsWithoutSideEffects()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam("Live", ExamStatus.Published);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var now = DateTime.UtcNow;
        // Attempts: 2 active, 1 expired-but-InProgress, 1 submitted.
        db.ExamAttempts.Add(NewAttempt(exam.Id, ExamAttemptStatus.InProgress, now.AddMinutes(30)));
        db.ExamAttempts.Add(NewAttempt(exam.Id, ExamAttemptStatus.InProgress, now.AddMinutes(30)));
        db.ExamAttempts.Add(NewAttempt(exam.Id, ExamAttemptStatus.InProgress, now.AddMinutes(-1)));
        db.ExamAttempts.Add(NewAttempt(exam.Id, ExamAttemptStatus.Submitted, now.AddMinutes(30)));
        // Queue: 2 waiting, 1 called within grace, 1 called past grace, 1 expired.
        db.WaitingQueueEntries.Add(NewQueueEntry(exam.Id, WaitingQueueStatus.Waiting));
        db.WaitingQueueEntries.Add(NewQueueEntry(exam.Id, WaitingQueueStatus.Waiting));
        db.WaitingQueueEntries.Add(NewQueueEntry(exam.Id, WaitingQueueStatus.Called, now.AddMinutes(-1)));
        var stale = NewQueueEntry(exam.Id, WaitingQueueStatus.Called, now.AddMinutes(-10));
        db.WaitingQueueEntries.Add(stale);
        db.WaitingQueueEntries.Add(NewQueueEntry(exam.Id, WaitingQueueStatus.Expired));
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLiveCountsQueryHandler(db);
        var result = await handler.Handle(new GetExamLiveCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value!);
        Assert.Equal(exam.Id, dto.ExamId);
        Assert.Equal(2, dto.ActiveAttempts);
        Assert.Equal(20, dto.MaxConcurrentAttempts);
        Assert.Equal(1, dto.ReservedCalled);   // the past-grace Called entry is excluded arithmetically
        Assert.Equal(2, dto.WaitingCount);

        // Read-only guarantee: the stale Called entry was NOT mutated to Expired.
        Assert.Equal(WaitingQueueStatus.Called, db.WaitingQueueEntries.Single(e => e.Id == stale.Id).Status);
    }

    [Fact]
    public async Task Handle_NonPublishedExams_AreNotReturned()
    {
        using var db = TestDbContextFactory.Create();
        db.Exams.Add(NewExam("Draft", ExamStatus.Draft));
        db.Exams.Add(NewExam("Closed", ExamStatus.Closed));
        db.Exams.Add(NewExam("Archived", ExamStatus.Archived));
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLiveCountsQueryHandler(db);
        var result = await handler.Handle(new GetExamLiveCountsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Handle_PublishedExamWithNoActivity_ReturnsZeros()
    {
        using var db = TestDbContextFactory.Create();
        var exam = NewExam("Quiet", ExamStatus.Published);
        db.Exams.Add(exam);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new GetExamLiveCountsQueryHandler(db);
        var result = await handler.Handle(new GetExamLiveCountsQuery(), CancellationToken.None);

        var dto = Assert.Single(result.Value!);
        Assert.Equal(0, dto.ActiveAttempts);
        Assert.Equal(0, dto.ReservedCalled);
        Assert.Equal(0, dto.WaitingCount);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter GetExamLiveCountsQueryHandlerTests`
Expected: FAIL to compile — the `GetExamLiveCounts` types do not exist yet.

- [ ] **Step 3: Implement DTO, query, and handler**

`src/ExamSystem.Application/Features/Exams/GetExamLiveCounts/ExamLiveCountsDto.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.GetExamLiveCounts;

/// <summary>Live batch-gate numbers for one Published exam (FR-8.8).</summary>
public record ExamLiveCountsDto(
    Guid ExamId,
    int ActiveAttempts,
    int MaxConcurrentAttempts,
    int ReservedCalled,
    int WaitingCount);
```

`src/ExamSystem.Application/Features/Exams/GetExamLiveCounts/GetExamLiveCountsQuery.cs`:
```csharp
namespace ExamSystem.Application.Features.Exams.GetExamLiveCounts;

public record GetExamLiveCountsQuery : IRequest<Result<List<ExamLiveCountsDto>>>;
```

`src/ExamSystem.Application/Features/Exams/GetExamLiveCounts/GetExamLiveCountsQueryHandler.cs`:
```csharp
using ExamSystem.Domain.Attempts;
using ExamSystem.Domain.Exams;
using ExamSystem.Domain.Queue;

namespace ExamSystem.Application.Features.Exams.GetExamLiveCounts;

/// <summary>
/// Read-only live counts per Published exam. Mirrors <see cref="Common.Interfaces.IQueueReconciler"/>
/// definitions arithmetically (grace-expired Called entries are excluded, not mutated) — this handler
/// must never write; reconciliation stays owned by the candidate-facing flow.
/// </summary>
public class GetExamLiveCountsQueryHandler : IRequestHandler<GetExamLiveCountsQuery, Result<List<ExamLiveCountsDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetExamLiveCountsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<ExamLiveCountsDto>>> Handle(GetExamLiveCountsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var published = await _db.Exams
            .Where(e => e.Status == ExamStatus.Published)
            .Select(e => new { e.Id, e.MaxConcurrentAttempts, e.GraceWindowMinutes })
            .ToListAsync(cancellationToken);

        if (published.Count == 0)
        {
            return Result<List<ExamLiveCountsDto>>.Success(new List<ExamLiveCountsDto>());
        }

        var examIds = published.Select(e => e.Id).ToList();

        var activeCounts = await _db.ExamAttempts
            .Where(a => examIds.Contains(a.ExamId) && a.Status == ExamAttemptStatus.InProgress && a.ExpiresAtUtc > now)
            .GroupBy(a => a.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExamId, x => x.Count, cancellationToken);

        var waitingCounts = await _db.WaitingQueueEntries
            .Where(q => examIds.Contains(q.ExamId) && q.Status == WaitingQueueStatus.Waiting)
            .GroupBy(q => q.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExamId, x => x.Count, cancellationToken);

        // Called entries need per-row CalledAtUtc because the grace window differs per exam.
        var calledEntries = await _db.WaitingQueueEntries
            .Where(q => examIds.Contains(q.ExamId) && q.Status == WaitingQueueStatus.Called)
            .Select(q => new { q.ExamId, q.CalledAtUtc })
            .ToListAsync(cancellationToken);

        var dtos = published
            .Select(e => new ExamLiveCountsDto(
                e.Id,
                activeCounts.GetValueOrDefault(e.Id),
                e.MaxConcurrentAttempts,
                calledEntries.Count(c =>
                    c.ExamId == e.Id &&
                    c.CalledAtUtc is { } calledAt &&
                    calledAt.AddMinutes(e.GraceWindowMinutes) > now),
                waitingCounts.GetValueOrDefault(e.Id)))
            .ToList();

        return Result<List<ExamLiveCountsDto>>.Success(dtos);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/ExamSystem.Application.UnitTests --filter GetExamLiveCountsQueryHandlerTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Commit**

```bash
git add src/ExamSystem.Application tests/ExamSystem.Application.UnitTests
git commit -m "feat(application): add read-only GetExamLiveCounts query (FR-8.8)"
```

---

### Task 2: API — live-counts endpoint + integration tests

**Files:**
- Modify: `src/ExamSystem.Api/Controllers/ExamsController.cs`
- Test: `tests/ExamSystem.Api.IntegrationTests/Controllers/ExamsControllerTests.cs`

- [ ] **Step 1: Write the failing integration tests**

Append inside the existing `ExamsControllerTests` class (uses the existing `TestWebApplicationFactory`, `CreateAuthenticatedAdminClientAsync`, `CreateTopicAsync`, `CreateMcqQuestionAsync`, `BuildExamPayload`, `IdResponse` helpers):

```csharp
    private sealed record LiveCountsResponse(Guid ExamId, int ActiveAttempts, int MaxConcurrentAttempts, int ReservedCalled, int WaitingCount);

    [Fact]
    public async Task LiveCounts_Anonymous_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/exams/live-counts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LiveCounts_Admin_ReturnsPublishedExamWithZeroActivity()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var topicId = await CreateTopicAsync(client, "Excel Skills - LiveCounts");
        await CreateMcqQuestionAsync(client, topicId, "Medium");

        var createResponse = await client.PostAsJsonAsync("/api/admin/exams", BuildExamPayload(topicId, 1));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();
        var publishResponse = await client.PostAsync($"/api/admin/exams/{created!.Id}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/admin/exams/live-counts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var counts = await response.Content.ReadFromJsonAsync<List<LiveCountsResponse>>();
        var row = Assert.Single(counts!, c => c.ExamId == created.Id);
        Assert.Equal(0, row.ActiveAttempts);
        Assert.Equal(20, row.MaxConcurrentAttempts);
        Assert.Equal(0, row.ReservedCalled);
        Assert.Equal(0, row.WaitingCount);
    }
```

Note: `LiveCounts_Admin_...` asserts with `Assert.Single(counts, c => c.ExamId == created.Id)` (not bare `Assert.Single`) because the shared factory DB may contain Published exams from other tests.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter LiveCounts`
Expected: FAIL — `404 NotFound` instead of `401`/`200` (route does not exist yet).

- [ ] **Step 3: Add the controller action**

In `src/ExamSystem.Api/Controllers/ExamsController.cs` add the using and action:

```csharp
using ExamSystem.Application.Features.Exams.GetExamLiveCounts;
```

```csharp
    /// <summary>Live batch-gate counts per Published exam (FR-8.8). Read-only; safe to poll.</summary>
    [HttpGet("live-counts")]
    public async Task<IActionResult> GetLiveCounts(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetExamLiveCountsQuery(), cancellationToken);
        return Ok(result.Value);
    }
```

Place it directly after the existing `GetAll` action. The `{id:guid}` route constraint on `GetById`
means the literal `live-counts` segment cannot collide.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/ExamSystem.Api.IntegrationTests --filter LiveCounts`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 5: Run the entire backend suite**

Run: `dotnet test`
Expected: all tests pass, no regressions.

- [ ] **Step 6: Commit**

```bash
git add src/ExamSystem.Api tests/ExamSystem.Api.IntegrationTests
git commit -m "feat(api): GET /api/admin/exams/live-counts endpoint (FR-8.8)"
```

---

### Task 3: Frontend — service method + polling badge in exams-list

**Files:**
- Modify: `frontend/src/app/core/services/exam.service.ts`
- Modify: `frontend/src/app/features/admin/exams/exams-list.component.ts`
- Modify: `frontend/src/app/features/admin/exams/exams-list.component.html`
- Modify: `frontend/src/styles/_surfaces.scss`

- [ ] **Step 1: Add the interface and service method**

In `frontend/src/app/core/services/exam.service.ts`, after the `ExamDetail` interface add:

```typescript
export interface ExamLiveCounts {
  examId: string;
  activeAttempts: number;
  maxConcurrentAttempts: number;
  reservedCalled: number;
  waitingCount: number;
}
```

and inside `ExamService` (after `clone`):

```typescript
  getLiveCounts(): Observable<ExamLiveCounts[]> {
    return this.http.get<ExamLiveCounts[]>(`${this.baseUrl}/live-counts`);
  }
```

- [ ] **Step 2: Add the polling loop to the component**

In `frontend/src/app/features/admin/exams/exams-list.component.ts`:

Replace the first four import lines with:

```typescript
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, catchError, switchMap, timer } from 'rxjs';
import { ExamDetail, ExamInput, ExamLiveCounts, ExamService, ExamSummary } from '../../../core/services/exam.service';
```

Inside the class, after `isFormOpen = signal(false);` add:

```typescript
  /** Live batch-gate counts per Published exam id (FR-8.8), refreshed by polling. */
  liveCounts = signal<Map<string, ExamLiveCounts>>(new Map());

  private static readonly LIVE_COUNTS_POLL_MS = 15_000;
  private readonly destroyRef = inject(DestroyRef);

  liveFor(examId: string): ExamLiveCounts | undefined {
    return this.liveCounts().get(examId);
  }
```

At the end of `ngOnInit()` add:

```typescript
    // FR-8.8: poll live counts; on error keep last values — the next tick retries.
    timer(0, ExamsListComponent.LIVE_COUNTS_POLL_MS)
      .pipe(
        switchMap(() => this.examService.getLiveCounts().pipe(catchError(() => EMPTY))),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(counts =>
        this.liveCounts.set(new Map(counts.map(c => [c.examId, c])))
      );
```

- [ ] **Step 3: Render the badge on Published rows**

In `frontend/src/app/features/admin/exams/exams-list.component.html`, replace the status cell:

```html
        <td>
          <span class="badge" [ngClass]="statusBadge(exam.status)">
            {{ statusLabel(exam.status) }}
          </span>
        </td>
```

with:

```html
        <td>
          <span class="badge" [ngClass]="statusBadge(exam.status)">
            {{ statusLabel(exam.status) }}
          </span>
          <span
            class="badge badge-live"
            *ngIf="exam.status === 'Published' && liveFor(exam.id) as lc"
            [title]="'محجوز (تم استدعاؤهم): ' + lc.reservedCalled"
          >
            نشطون {{ lc.activeAttempts }}/{{ lc.maxConcurrentAttempts }} · طابور {{ lc.waitingCount }}
          </span>
        </td>
```

- [ ] **Step 4: Add the `.badge-live` style**

In `frontend/src/styles/_surfaces.scss`, after the `.badge-danger, .badge-archived` block add:

```scss
.badge-live {
  margin-inline-start: var(--space-xs);
  color: var(--authority-blue-deep);
  background: var(--authority-blue-tint);
  font-variant-numeric: tabular-nums;
}
```

- [ ] **Step 5: Build the frontend**

Run: `cd frontend && npx ng build --configuration development`
Expected: build succeeds with no template/type errors.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/core/services/exam.service.ts frontend/src/app/features/admin/exams frontend/src/styles/_surfaces.scss
git commit -m "feat(admin-ui): live batch-gate counts badge on published exams (FR-8.8)"
```

---

### Task 4: Manual verification (preview)

- [ ] **Step 1: Run both servers** (`.claude/launch.json` configs `api` + `frontend`; see memory note `dev-proxy-and-servers`).

- [ ] **Step 2: Verify in the admin exams page**

1. Log in as admin (`admin / ChangeMe!2026`), open `/admin/exams`.
2. Published rows show the live badge «نشطون 0/20 · طابور 0» (numbers per seeded data); Draft/Closed/Archived rows show only the status badge.
3. Confirm via devtools/network that `GET /api/admin/exams/live-counts` fires on load and every ~15s, and returns 200.
4. Navigate away to `/admin/topics` — confirm polling requests stop.

- [ ] **Step 3: Full backend suite one last time**

Run: `dotnet test`
Expected: all green.

---

## Spec Coverage Summary

| Spec section | Task |
|---|---|
| §3.1 Query (read-only, grouped, per-exam grace) | Task 1 |
| §3.2 API endpoint + auth + no-collision note | Task 2 |
| §4.1 Service method | Task 3 Step 1 |
| §4.2 Badge, 15s polling, takeUntilDestroyed, error handling | Task 3 Steps 2–4 |
| §5 Testing (unit incl. no-side-effects; integration 401/200) | Task 1 Step 1, Task 2 Step 1 |
| §6 Out of scope | (nothing else touched) |
