# Design Spec — Candidate Exam, Slice 3: Reliability & Anti-Cheat Hardening

- **Date:** 2026-07-06
- **Status:** Approved (design), pending implementation plan
- **Feature:** Candidate-facing exam surface (Ministry of Justice — Judicial Information Center)
- **Scope unit:** Slice 3 of the candidate feature (follows 1a + 1b + 2)
- **Depends on:** Slices 1a, 1b, 2 — all DONE on `master`

---

## 1. Context

The candidate can register, be batch-gated through a waiting queue, take an exam, submit, and see a
result. Two reliability/security requirements from the PRD remain, plus best-effort anti-cheat:

- **FR-2.7** — auto-submit expired attempts even when nobody is hitting the server. Today only the
  *lazy* auto-submit exists (fires on the next request touching that attempt); an idle attempt with no
  further requests stays `InProgress` and keeps holding a batch slot.
- **FR-8.9** — IP-based rate limiting on the public registration/start endpoints to prevent queue flooding.
- **Anti-cheating (basic)** — disable copy/paste + right-click on the exam surface (best effort) and log
  tab-switches as an integrity indicator.

Slice 3 delivers all three as independent hardening subsystems. Nothing changes in the answering/grading
logic itself.

### Scope decisions (from brainstorming)
- All three parts are in scope: background auto-submit, rate limiting, anti-cheat + tab-switch logging.
- **Tab-switch is a count only** (`ExamAttempt.TabSwitchCount`), not a detailed event log.
- **Deferred:** OTP (FR-1.6, "Could"); surfacing the tab-switch count / performance metrics in an admin
  report (FR-6 reporting is unbuilt) — the count is stored now and displayed when reports are built.

---

## 2. Boundary of Slice 3

**In scope**
- **A. Background auto-submit:** a hosted `BackgroundService` that periodically closes + grades every
  `InProgress` attempt past its `ExpiresAtUtc`, supplementing the existing lazy auto-submit.
- **B. Rate limiting:** ASP.NET Core rate limiter on the public candidate endpoints (`register`, `start`,
  `queue/status`), keyed by client IP; over-limit requests get `429`.
- **C. Anti-cheat + tab-switch count:** `ExamAttempt.TabSwitchCount` + a token-authenticated endpoint to
  increment it; the exam player disables copy/paste/right-click and reports tab-switches.

**Out of scope (deferred)**
- OTP before starting (FR-1.6).
- Admin reporting UI that surfaces the tab-switch count / P95 latency (FR-6).
- Any change to 1b answering/submit/grading or Slice 2 queue logic.
- The free-tier host may sleep; when asleep neither the background job nor requests run — the lazy
  auto-submit remains the ultimate fallback on the next wake/request (documented, not "fixed" here).

---

## 3. Part A — Background auto-submit (FR-2.7)

- **`IExpiredAttemptCloser`** (Application interface) + `ExpiredAttemptCloser` (Infrastructure impl):
  `Task<int> CloseExpiredAsync(DateTime nowUtc, CancellationToken)` finds `InProgress` attempts with
  `ExpiresAtUtc <= now` (loaded with `Questions`→`Options` and `Answers` and the `Exam`), grades each via
  the existing `IAttemptGradingService`, sets `Status = AutoSubmitted`, `SubmittedAtUtc = ExpiresAtUtc`,
  and `Score`, then saves. Returns the number closed. This is the unit-testable core.
- **`ExpiredAttemptSubmissionService : BackgroundService`** (API host): every `AutoSubmit:IntervalSeconds`
  (config, default 60) it creates a DI scope, resolves `IExpiredAttemptCloser`, and calls
  `CloseExpiredAsync(DateTime.UtcNow)`. Exceptions are caught and logged so one bad tick doesn't kill the
  loop. Registered with `AddHostedService` (Development already runs migrations/seed on startup; the
  service starts with the app).
- **Relationship to lazy auto-submit:** identical outcome; the background job just guarantees it happens
  without a triggering request, which also frees the batch slot promptly for Slice 2.

---

## 4. Part B — Rate limiting (FR-8.9)

- Register the built-in rate limiter (`builder.Services.AddRateLimiter(...)`) with a named policy
  `"candidate"`: a **fixed-window** limiter, partitioned by the client IP
  (`HttpContext.Connection.RemoteIpAddress`, falling back to a constant partition key when the IP is
  unavailable so tests and proxies behave deterministically). Default window: **20 permits / 1 minute**
  (config `RateLimiting:Candidate:PermitLimit` and `:WindowSeconds`). Rejected requests return **429**.
- Add `app.UseRateLimiter()` to the pipeline (after `UseRouting`/before endpoints).
- Apply `[EnableRateLimiting("candidate")]` to `CandidateExamController` (covers `landing`/`register`/
  `start`) and `CandidateQueueController` (covers `queue/status`). The admin and 1b attempt endpoints are
  not rate-limited by this policy.

---

## 5. Part C — Anti-cheat + tab-switch count

### Backend
- **`ExamAttempt.TabSwitchCount`** (int, default 0) + EF mapping (no special config needed) + migration.
- **`RecordTabSwitchCommand(Guid AttemptId)`** + handler: increments `TabSwitchCount` only when the attempt
  is `InProgress` (ignore otherwise); returns success either way (fire-and-forget from the client).
- **`POST /api/exam/{examId}/attempt/tab-switch`** on `CandidateAttemptController`
  (`[Authorize(AuthenticationSchemes = "AttemptToken")]`, resolves the attempt id from the token like the
  other 1b actions); returns `204`.

### Frontend (`AttemptPlayerComponent`)
- While the attempt is `InProgress`:
  - `document.addEventListener('visibilitychange', ...)`: when `document.hidden` becomes true, call
    `CandidateAttemptService.recordTabSwitch(examId)` (throttled so a rapid switch storm sends at most one
    call per few seconds).
  - Prevent-default handlers on `contextmenu`, `copy`, `cut`, and `paste` for the player, disabling
    right-click and copy/paste (best-effort).
  - All listeners are removed in `ngOnDestroy`.
- **`CandidateAttemptService.recordTabSwitch(examId)`** → `POST .../attempt/tab-switch` (token attached by
  the existing attempt-token interceptor). Errors are ignored (integrity logging must never block the exam).
- These measures are explicitly best-effort (trivially bypassable by a determined user); they are an
  integrity signal, not a hard control.

---

## 6. Error Handling & Edge Cases

- **Background tick failure** → caught + logged; the loop continues on the next interval.
- **Concurrent close** (background job closes an attempt at the same moment a request lazily closes it) →
  both paths only act on `InProgress`; whichever writes first wins and the other finds it already closed
  (idempotent — no double grading of consequence since the grade is deterministic).
- **Rate-limit false positives** (shared NAT IP) → acceptable for v1 at 20/min; the limit is config-tunable.
- **Tab-switch after submission** → the command no-ops (not `InProgress`).
- **Clipboard/anti-cheat unsupported** → listeners simply have no effect; never throws.

---

## 7. Testing

- **Part A (unit):** `ExpiredAttemptCloser` closes + grades an expired `InProgress` attempt (status →
  `AutoSubmitted`, score set), and leaves a not-yet-expired `InProgress` attempt and an already-`Submitted`
  attempt untouched; returns the correct closed count.
- **Part B (integration):** exceeding the candidate rate limit on `register` returns `429` within one
  window (set a low limit in the test config to keep it fast and deterministic).
- **Part C (unit):** `RecordTabSwitchCommand` increments only while `InProgress`; no-ops otherwise.
- **Part C (frontend):** a spec asserting that a `visibilitychange` to hidden calls
  `service.recordTabSwitch`, and that the player registers/removes its listeners.
- TDD applied where practical.

---

## 8. Open Items / Follow-ups (not blocking Slice 3)

- Admin report that surfaces `TabSwitchCount` (and P95/P99 latency, FR "Continuous Performance
  Monitoring") — part of the future reporting slice (FR-6).
- OTP before start (FR-1.6, "Could").
- Daily report aggregation background job (PRD §5.1) — a separate reporting-slice concern.
