# Design Spec — Candidate Exam, Slice 2: Batch Gate / Waiting Room (Auto mode)

- **Date:** 2026-07-06
- **Status:** Approved (design), pending implementation plan
- **Feature:** Candidate-facing exam surface (Ministry of Justice — Judicial Information Center)
- **Scope unit:** Slice 2 of the candidate feature (follows 1a + 1b)
- **Depends on:** Slice 1a (entry/start) and 1b (exam engine) — both DONE on `master`

---

## 1. Context

Slices 1a and 1b are complete: a candidate registers, starts an attempt (server timer + snapshot +
`AttemptToken`), takes the exam, submits, and sees the result. Today, **`start` always creates an
attempt** — there is no capacity limit.

Slice 2 adds the **Batch Gate** (FR-8): the system runs the exam in batches of at most
`MaxConcurrentAttempts` concurrent takers (default 20) so it stays responsive on free-tier hosting. When a
batch is full, a new candidate joins a **FIFO waiting queue** and sees a **waiting room** that polls their
position; as active attempts finish, waiting candidates are promoted and started, one at a time, honoring
order.

Grounded in `PRD-Exam-System_2.md` FR-8.1–8.6 and §5.1 (polling, server-side truth, no SignalR dependency).

### Scope decisions (from brainstorming)
- **Auto mode only.** Manual "open next batch" (FR-8.7) and the admin live-counts dashboard (FR-8.8) are
  **deferred** to a follow-up. `QueueMode` is not introduced yet (only Auto exists).
- **Admin-configurable capacity.** `MaxConcurrentAttempts` and `GraceWindowMinutes` are added to the `Exam`
  entity, its create/update commands, and the admin exam form (FR-8.1).

---

## 2. Boundary of Slice 2

**In scope**
- `Exam.MaxConcurrentAttempts` (default 20) and `Exam.GraceWindowMinutes` (default 3), settable by the admin.
- Capacity check at the single `start` entry point: create an attempt if a slot is free, else enqueue.
- `WaitingQueueEntry` persistence (FIFO, keyed by national ID per exam).
- A queue-status endpoint the waiting room polls (anonymous, identified by national ID — FR-8.6).
- **Lazy reconciliation**: on each `start` / `queue-status` request, expire timed-out `Called` reservations
  and promote the earliest `Waiting` candidates while capacity allows. No background job, no changes to 1b.
- Waiting room UI: position + rough ETA, ~20s polling, auto-start when it is the candidate's turn.

**Out of scope (deferred)**
- Manual queue mode / "open next batch" (FR-8.7).
- Admin live-counts dashboard (FR-8.8).
- Register rate limiting (FR-8.9) → Slice 3.
- Any change to the 1b answering/submit/grading flow.

---

## 3. Core Model — capacity & lazy reconciliation

- **Active count** for an exam = number of `ExamAttempt` with `Status == InProgress` **and**
  `ExpiresAtUtc > now`. An attempt that is submitted, auto-submitted, or simply time-expired **stops
  counting automatically** — so a finished attempt frees its slot implicitly, with no explicit "release".
- **Reserved count** = number of `WaitingQueueEntry` with `Status == Called` whose grace window has not
  elapsed (`CalledAtUtc + GraceWindowMinutes > now`).
- **Available slots** = `MaxConcurrentAttempts - ActiveCount - ReservedCount` (floored at 0).
- **`IQueueReconciler`** runs at the start of every `start` and `queue-status` request:
  1. Mark each `Called` entry whose grace has elapsed as `Expired` (its reserved slot is released).
  2. While `AvailableSlots > 0` and a `Waiting` entry exists, promote the earliest (`EnqueuedAtUtc`) to
     `Called`, stamp `CalledAtUtc`, decrement available slots.
  3. Recompute `Position` for the remaining `Waiting` entries (1-based over `EnqueuedAtUtc`).

This mirrors the existing lazy-auto-submit pattern: the next candidate's poll drives the whole thing, so
Slice 1b needs no modification and no hosted service is required.

---

## 4. Backend Architecture (Clean Architecture + CQRS/MediatR)

### 4.1 Domain
- **`Exam`** (modify): add `int MaxConcurrentAttempts = 20;` and `int GraceWindowMinutes = 3;`.
- **`WaitingQueueEntry`** (new): `Id, ExamId, CandidateId, EnqueuedAtUtc, Position, CalledAtUtc?`, and
  `Status` (`WaitingQueueStatus`: `Waiting, Called, Started, Expired, Cancelled`).

### 4.2 Application
- **`IQueueReconciler`** (interface + Infrastructure impl): `ReconcileAsync(Guid examId, CancellationToken)`
  performs the three reconciliation steps above and persists changes. Returns nothing; callers re-read.
- **`StartAttemptCommand` (modify)** — single entry point, after existing identity/resolve/already-taken logic:
  1. If an `InProgress` attempt exists → resume (return `Started`, unchanged).
  2. `ReconcileAsync(examId)`.
  3. If the candidate has a non-expired `Called` entry **or** `AvailableSlots > 0` and no other candidate is
     ahead in `Called`/`Waiting`: create the attempt (snapshot + timer + token as today); mark the
     candidate's queue entry (if any) `Started`. Return `Started`.
  4. Else: upsert a `Waiting` entry for this (exam, candidate) if none active; return `Queued` with the
     candidate's current `Position`.
- **`StartAttemptResult`** shape widens to represent the two outcomes:
  `record StartAttemptDto(string Outcome /* "Started" | "Queued" */, Guid? AttemptId, string? AttemptToken, DateTime? ExpiresAtUtc, int? QueuePosition)`.
- **`GetQueueStatusQuery(Guid ExamId, string NationalId)`** → `ReconcileAsync`, then resolve the candidate's
  entry: returns `QueueStatusDto(string Status /* "Waiting" | "Called" | "Started" | "Expired" | "NotQueued" */, int Position, int EstimatedWaitSeconds)`.
  - `EstimatedWaitSeconds` is a coarse estimate: `ceil(Position / MaxConcurrentAttempts) * DurationMinutes * 60`.
- **`ExamTopicSelectionInput` / create/update commands (modify)**: `CreateExamCommand` and
  `UpdateExamCommand` gain `MaxConcurrentAttempts` and `GraceWindowMinutes`; validators enforce
  `MaxConcurrentAttempts >= 1` and `GraceWindowMinutes >= 1`. `GetExamByIdQuery`'s DTO returns them.

### 4.3 Infrastructure
- EF config + migration: `WaitingQueueEntry` (index on `(ExamId, Status, EnqueuedAtUtc)`, and
  `(ExamId, CandidateId)`), plus the two new `Exam` columns.
- `QueueReconciler` implementation; DI registration.
- `DbSet<WaitingQueueEntry>` on `IApplicationDbContext` + `ApplicationDbContext`.

### 4.4 API
- **`CandidateQueueController`** (`api/exam/{examId:guid}/queue`, `[AllowAnonymous]`):
  `GET /status?nationalId=...` → `QueueStatusDto`. (Identified by national ID so it survives disconnect —
  FR-8.6.)
- **`start`** stays at `POST /api/exam/{examId}/start` (existing `CandidateExamController`); its response body
  is now the widened `StartAttemptDto`. No separate `queue/start` endpoint.

### 4.5 Grace-expiry behavior
When a `Called` reservation's grace elapses, the entry becomes `Expired` and the slot is released to the
next `Waiting` candidate. An expired candidate who returns (polls status or presses start) is **re-enqueued
at the tail** (a fresh `Waiting` entry / new `EnqueuedAtUtc`) — i.e. "back to the end of the queue", not a
hard cancel.

---

## 5. Frontend (Candidate Area)

- **`start` handling (modify `InstructionsComponent`)**: on `start`, branch on `outcome`:
  `Started` → store the attempt token and go to the player (as today); `Queued` → go to the waiting room.
- **`WaitingRoomComponent`** (new, route `/exam/:examId/waiting`): shows "ترتيبك في الطابور: N" and a rough
  ETA; polls `GET queue/status` every ~20s using the candidate's national ID (persisted client-side for the
  exam so polling survives reload — FR-8.6). When status becomes `Called`, it automatically calls `start`
  within the grace window → `Started` → navigate to the player. If the candidate's entry is `Expired`, it
  re-calls `start` (which re-enqueues) and keeps polling.
- **Identity persistence**: the candidate's registration identity (national ID, name, mobile) is stored
  client-side (e.g. `localStorage["queue_{examId}"]`) when entering the queue, so the waiting room can poll
  and auto-start after a refresh or disconnect. Cleared once `Started`.
- **`CandidateExamService` (modify)**: `start` returns the widened result; add `queueStatus(examId, nationalId)`.

---

## 6. Error Handling & Edge Cases

- **Disconnect / reload while waiting** → identity from `localStorage` lets the waiting room resume polling;
  position is unchanged (server keeps the entry keyed by national ID).
- **Grace expiry** → entry `Expired`; next action re-enqueues at the tail (§4.5).
- **Idempotency** → repeated `start` while queued returns the same `Queued` position; repeated `start` while
  `Called`/capacity-available creates exactly one attempt; repeated `queue/status` is read-plus-reconcile.
- **Already-taken / not-open** (from 1a) still short-circuit before any queueing.
- **Capacity = generous / queue empty** → `start` behaves exactly as in 1a (immediate attempt), so nothing
  regresses for under-capacity exams.

---

## 7. Testing

- **Backend unit (`QueueReconciler`)**: promotes the earliest Waiting when a slot frees; respects FIFO order;
  expires a `Called` entry past its grace and promotes the next; a time-expired `InProgress` attempt no
  longer counts toward capacity; positions recompute correctly.
- **Backend unit (`StartAttemptCommand`)**: under capacity → `Started` with a token; at capacity →
  `Queued` with position; a valid `Called` reservation → `Started`; an expired `Called` → re-enqueued;
  repeated start while queued is idempotent.
- **Backend unit (`GetQueueStatusQuery`)**: returns the candidate's status/position; `Called` once promoted;
  `NotQueued` for an unknown candidate.
- **Backend integration** (`MaxConcurrentAttempts = 1`): candidate A `start` → `Started`; candidate B
  `start` → `Queued` position 1; A submits (1b) → B `GET queue/status` → `Called` → B `start` → `Started`.
  Admin can set `MaxConcurrentAttempts`/`GraceWindowMinutes` via create/update and read them back.
- **Frontend**: instructions branches to the waiting room on `Queued`; waiting room renders position, polls
  status, and navigates to the player when `Called` resolves to `Started`.
- TDD applied where practical.

---

## 8. Open Items / Follow-ups (not blocking Slice 2)

- Manual queue mode + "open next batch" (FR-8.7) and admin live-counts dashboard (FR-8.8) — a later slice;
  will introduce `QueueMode` on `Exam`.
- Register rate limiting (FR-8.9) → Slice 3.
- The ETA heuristic (§4.2) is deliberately coarse; refine if real turnover data becomes available.
