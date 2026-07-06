# Manual Queue Mode — "Open Next Batch" (FR-8.7) — Design

**Date:** 2026-07-07
**Status:** Approved (brainstorming with user)
**PRD:** `PRD-Exam-System_2.md` FR-8.7 (Should) — «وضع بديل يدوي: الأدمن يتحكم في فتح الدفعات بنفسه (زر "افتح الدفعة التالية") بدلًا من التحرير التلقائي — Configurable».

## 1. Goal

Give the admin a per-exam **Manual** queue mode in which no candidate enters the exam without an
explicit admin release. The admin opens batches with a count-controlled «افتح الدفعة التالية» button.
Auto mode (today's behavior) stays the default and is untouched.

## 2. Decisions (confirmed with user)

1. **Strict manual semantics.** In Manual mode, *all* entry requires admin release: a new arrival
   always enqueues — even when free capacity exists. The only path into the exam is being promoted
   to `Called` by the admin's button (the grace window then applies exactly as in Auto, FR-8.5).
   The softer alternative (capacity entry stays automatic, only queue promotion becomes manual)
   was considered and rejected.
2. **Admin-entered batch count.** The button takes a number (how many to call). The effective count
   is always capped: `called = min(requestedCount, availableCapacity, waitingCount)`. Capacity can
   never be exceeded (FR-8.1 invariant preserved).
3. **Mode is live-togglable.** `QueueMode` is set in the exam form (default Auto) *and* switchable
   while the exam is Published via a dedicated endpoint (like the lifecycle actions). Rationale: an
   admin must be able to flip to Manual mid-exam if hosting resources degrade. Draft-form-only
   configuration was considered and rejected.

Rejected architecture alternatives: strategy pattern with two reconciler implementations
(over-engineering for one behavioral branch); a global (non-per-exam) setting (contradicts the PRD).

## 3. Domain

- New enum `src/ExamSystem.Domain/Queue/QueueMode.cs`: `Auto = 0, Manual = 1`.
- `Exam.QueueMode` property, default `QueueMode.Auto`.
- EF migration `AddExamQueueMode` (int column, default 0; no backfill needed — 0 = Auto is correct
  for every existing row).

## 4. Queue behavior changes (surgical)

### 4.1 `QueueReconciler.ReconcileAsync`

Current steps: (1) expire grace-timed-out Called, (2) promote earliest Waiting while capacity
allows, (3) recompute Waiting positions.

Change: **step 2 runs only when `exam.QueueMode == Auto`.** Steps 1 and 3 run in both modes
(grace expiry and honest positions are mode-independent). Return type `QueueCapacity` unchanged.

### 4.2 `IQueueReconciler` gains one method

```csharp
Task<int> CallNextBatchAsync(Guid examId, int maxToCall, CancellationToken cancellationToken);
```

Implemented in `QueueReconciler` (same class — promotion semantics stay in one place): expire
stale grace reservations first, compute `available`, promote the earliest
`min(maxToCall, available, waitingCount)` Waiting entries to `Called` (+`CalledAtUtc = now`),
recompute positions, save, and return how many were called. Works regardless of mode (it *is* the
manual promotion primitive; in Auto exams the admin has no button, so it is only reached in Manual).

### 4.3 `StartAttemptCommandHandler` entry gate

Line ~84 today: `if (called is not null || capacity.Available > 0)`.

Becomes: `if (called is not null || (exam.QueueMode == QueueMode.Auto && capacity.Available > 0))`.

In Manual mode only `Called` candidates may start; everyone else takes the existing enqueue path.
Resume of an InProgress attempt and the already-taken guard are untouched (they sit before the gate).

## 5. Application — new commands (CQRS, under `Features/Exams/`)

### 5.1 `OpenNextBatch/OpenNextBatchCommand(Guid ExamId, int Count)` → `Result<OpenBatchResultDto>`

- Failures: exam not found; `Status != Published` ("Exam is not published."); `QueueMode != Manual`
  ("Exam queue is not in manual mode."); `Count < 1` (validator).
- Happy path: delegates to `IQueueReconciler.CallNextBatchAsync(ExamId, Count, ct)`, then reads the
  post-call numbers and returns `OpenBatchResultDto(int CalledCount, int RemainingWaiting, int AvailableAfter)`
  (UI toast: «تم استدعاء N»). Calling with an empty queue succeeds with `CalledCount = 0`.

### 5.2 `SetQueueMode/SetExamQueueModeCommand(Guid ExamId, QueueMode Mode)` → `Result<Unit>`

- Allowed when `Status is Draft or Published`; rejected for Closed/Archived
  ("Queue mode can only be changed for draft or published exams.").
- Idempotent: setting the current mode succeeds without error.
- Manual→Auto: no immediate promotion inside the command; the next reconcile (first candidate poll
  or start) auto-promotes naturally. Auto→Manual: stops future auto-promotion; already-Called
  entries keep their grace window.

### 5.3 `QueueMode` joins the exam configuration surface

- `CreateExamCommand` / `UpdateExamCommand` + validators: new `QueueMode` field (enum; no extra
  validation rules — invalid enum values are rejected by model binding).
- `ExamSummaryDto` (list) and `ExamDetailDto` (detail) gain `QueueMode` so the admin UI can render
  mode indicators and the batch button.

## 6. API (`ExamsController`, auth inherited `[Authorize(Roles="Admin")]`)

- `POST /api/admin/exams/{id:guid}/queue-mode` — body `{ "mode": "Auto" | "Manual" }`,
  204 on success / 400 with `{errors}` on failure (matches existing lifecycle actions' style).
- `POST /api/admin/exams/{id:guid}/queue/open-batch` — body `{ "count": n }`,
  200 with `OpenBatchResultDto` / 400 with `{errors}`.

## 7. Frontend (admin exams-list + exam form; candidate side: **zero changes**)

- `exam.service.ts`: `queueMode` on `ExamSummary`/`ExamDetail`/`ExamInput`; methods
  `setQueueMode(id, mode)` and `openNextBatch(id, count)`.
- **Exam form:** a two-option select «وضع الطابور» (تلقائي / يدوي), default تلقائي, alongside the
  existing `maxConcurrentAttempts`/`graceWindowMinutes` fields.
- **Exams-list, Published rows only:**
  - Mode chip next to the live badge: «طابور تلقائي» / «طابور يدوي» + a toggle button
    («تحويل ليدوي» / «تحويل لتلقائي») calling the queue-mode endpoint, then refreshing the list.
  - When Manual: a small number input (default 1, min 1) + button «افتح الدفعة التالية», disabled
    while the FR-8.8 live badge shows `طابور 0`. On success: inline message «تم استدعاء N» (reuses
    the copy-link feedback pattern) and the live badge picks up the new numbers on its next poll.
- Waiting-room UX is already compatible: it polls queue status and auto-starts on `Called`.

## 8. Testing

- **Reconciler unit tests:** Manual exam — reconcile does NOT promote Waiting even with free
  capacity, but still expires stale Called and recomputes positions; Auto behavior unchanged
  (existing tests stay green). `CallNextBatchAsync`: promotes `min(count, available, waiting)`
  earliest-first; expires stale grace before computing availability; returns the called count.
- **StartAttempt unit tests:** Manual + free capacity + not called → `Queued`; Manual + `Called` →
  starts (and marks entry `Started`); Auto path regression-covered by existing tests.
- **Command unit tests:** OpenNextBatch — rejects Draft/Closed/Archived, rejects Auto-mode exams,
  empty queue → success with 0; SetQueueMode — toggles Draft & Published, rejects Closed/Archived,
  idempotent same-mode set.
- **Integration tests:** both endpoints — anonymous 401; admin happy path (set Manual on a
  published exam → open batch → 200 with shape).

## 9. Out of scope

- FR-8.5's "requeue at tail instead of cancel" configurability (expired grace keeps today's
  Expired behavior).
- Any change to FR-8.8 live counts (the badge already reflects Called/Waiting correctly).
- Notifications/SignalR; candidate-facing UI changes; batch scheduling/timers.
- Persisting an audit trail of batch opens (FR-7.4 audit log is a separate feature).
