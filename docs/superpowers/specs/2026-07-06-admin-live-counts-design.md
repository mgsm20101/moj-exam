# Admin Live Counts (FR-8.8) — Design

**Date:** 2026-07-06
**Status:** Approved (brainstorming with user)
**PRD:** `PRD-Exam-System_2.md` FR-8.8 (Should) — «لوحة الأدمن تعرض لحظيًا: عدد الممتحنين النشطين / سعة الدفعة / طول الطابور».

## 1. Goal

Show the admin, per **Published** exam, live numbers: active candidates, batch capacity, reserved
(called, grace pending) and queue length — inside the existing exams-list page, refreshed by polling.
No SignalR (PRD §5.1 mandates polling / server-side truth).

## 2. Decisions (confirmed with user)

1. **Placement: inside `/admin/exams` (exams-list)** — live badges on Published rows. No new page,
   no new nav item. A dedicated monitoring page was considered and rejected for now (YAGNI; can be
   added when the rest of FR-6 live monitoring is built).
2. **Read-only endpoint** — the admin poll does **not** run `IQueueReconciler`. It computes effective
   numbers with the same definitions the reconciler uses, without persisting anything. Real
   reconciliation keeps happening on every candidate start/queue-status poll (every ~20s while anyone
   waits). Rationale: GET stays side-effect-free, no write amplification from admin dashboards left
   open, and staleness is bounded by the candidates' own polling cadence. A reconciling variant was
   considered and rejected.

## 3. Backend

### 3.1 Query — `Features/Exams/GetExamLiveCounts/`

- `ExamLiveCountsDto(Guid ExamId, int ActiveAttempts, int MaxConcurrentAttempts, int ReservedCalled, int WaitingCount)`
- `GetExamLiveCountsQuery : IRequest<Result<List<ExamLiveCountsDto>>>` — no parameters; returns one
  row per **Published** exam only (Draft/Closed/Archived have no meaningful live numbers).
- `GetExamLiveCountsQueryHandler` — read-only, mirrors `QueueReconciler` definitions exactly:
  - `ActiveAttempts` = count of `ExamAttempts` where `Status == InProgress && ExpiresAtUtc > now`.
  - `ReservedCalled` = count of `WaitingQueueEntries` where `Status == Called` **and**
    `CalledAtUtc + Exam.GraceWindowMinutes > now` (grace-expired Called entries are excluded
    arithmetically — they are *not* mutated; the reconciler will expire them on the next candidate poll).
  - `WaitingCount` = count of `WaitingQueueEntries` where `Status == Waiting`.
  - Implementation: load Published exams, then bulk queries filtered to those exam ids — no N+1
    regardless of exam count: (a) active-attempt counts via `GroupBy(ExamId)`, (b) Waiting counts via
    `GroupBy(ExamId)`, (c) Called entries projected as `(ExamId, CalledAtUtc)` so the grace comparison
    happens in memory per exam (grace window differs per exam).

### 3.2 API

- `GET /api/admin/exams/live-counts` — new action on the existing `ExamsController`
  (`[Route("api/admin/exams")]`, `[Authorize(Roles = "Admin")]` already at controller level).
- Returns `200` with `List<ExamLiveCountsDto>` (empty list when no Published exams). No failure modes
  beyond auth — the query cannot 404.
- Note: the literal segment `live-counts` cannot collide with `{id:guid}` (guid route constraint).

## 4. Frontend

### 4.1 Service

- `core/services/exam.service.ts`: add `ExamLiveCounts` interface + `getLiveCounts(): Observable<ExamLiveCounts[]>`
  hitting `/api/admin/exams/live-counts`.

### 4.2 Exams-list component

- On Published rows only: a live badge — «نشطون {active}/{capacity} · طابور {waiting}» (ReservedCalled
  is folded into the tooltip/title as «محجوز: N» to keep the badge compact).
- Polling: RxJS `timer(0, 15_000)` + `switchMap(() => service.getLiveCounts())`, subscription managed
  with `takeUntilDestroyed` — starts on page load, stops on navigation away. 15s sits in the PRD's
  15–30s polling band.
- State: counts stored in a `Map<examId, ExamLiveCounts>` signal; rows look up by id; non-Published
  rows render nothing.
- **Error handling:** on request failure keep the last-known values (no toast spam, no table
  breakage); the next 15s tick retries naturally. First-load failure ⇒ badges simply absent.

## 5. Testing

- **Unit — `GetExamLiveCountsQueryHandlerTests`:**
  1. Seeds a Published exam with: 2 active attempts, 1 expired-but-InProgress attempt, 1 Submitted
     attempt; queue entries: 2 Waiting, 1 Called within grace, 1 Called past grace, 1 Expired.
     Asserts `ActiveAttempts == 2`, `ReservedCalled == 1`, `WaitingCount == 2`.
  2. Draft exam is not returned.
  3. **No side effects:** after handling, the grace-expired Called entry still has
     `Status == Called` (handler must not mutate/persist).
- **Integration — `ExamsControllerTests`:** anonymous → 401; admin → 200 with expected counts shape.

## 6. Out of scope

- FR-8.7 manual queue mode / "open next batch" (will introduce `QueueMode` later).
- Any change to `QueueReconciler`, candidate endpoints, or the waiting-room UI.
- SignalR / push updates.
- Admin dashboard (`/admin/dashboard`) cards — exams-list only.
