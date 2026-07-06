# Design Spec — Candidate Exam, Slice 1b: Exam Engine

- **Date:** 2026-07-06
- **Status:** Approved (design), pending implementation plan
- **Feature:** Candidate-facing exam surface (Ministry of Justice — Judicial Information Center)
- **Scope unit:** Slice 1b of the candidate feature (follows 1a)
- **Depends on:** Slice 1a (`docs/superpowers/specs/2026-07-06-candidate-exam-slice-1a-design.md`) — DONE

---

## 1. Context

Slice 1a is complete and on `master`: a candidate can open a public exam link, register, and start an
attempt. Starting an attempt creates an `ExamAttempt` (Status `InProgress`, server-side `ExpiresAtUtc`),
an immutable `AttemptQuestion (+Option)` snapshot, and issues an `AttemptToken` JWT (claims `attempt_id`,
`candidate_id`, `exam_id`). The candidate lands on a placeholder attempt shell.

**Slice 1b builds the exam engine**: rendering the snapshot questions, answering with auto-save, mid-exam
resume, a server-authoritative timer with lazy auto-submit, final submission, automatic grading, and the
result screen. It replaces the 1a attempt-shell placeholder with the real player.

Grounded in `PRD-Exam-System_2.md`: FR-2.5 (immutable snapshot — already built), FR-2.6 (server-side
timer as source of truth), FR-2.7 (lazy auto-submit fallback), FR-2.8 (auto-save per answer), FR-2.9
(resume), FR-2.10 (navigator + next/prev), FR-2.11 (flag for review), FR-2.12 (submit confirmation with
unanswered count), FR-2.13 (auto-grading), FR-2.14 (result visibility), and the auto-grading rules table
(§ "أنواع الأسئلة وقواعد التصحيح").

---

## 2. Boundary of 1b

**In scope**
- `GET attempt state`: sanitized questions (no correctness data) + saved answers + flags + remaining
  seconds + status. Used on first load and on resume.
- Per-answer auto-save: MCQ selection (immediate) and FillBlank text (debounced client-side, ~1s);
  flag-for-review toggle.
- One-question-per-screen player with next/prev and a full question navigator
  (answered / unanswered / flagged, with jump-to-question).
- Server-authoritative timer: countdown derived from `ExpiresAtUtc`; expiry re-checked on every
  state/save/submit request; **lazy auto-submit** closes and grades an expired attempt on the next request.
- Submit confirmation (shows unanswered count) → final submit → auto-grading → `Status` transitions to
  `Submitted` (or `AutoSubmitted` when triggered by expiry), `SubmittedAtUtc` and `Score` set.
- Result screen honoring `Exam.ShowResultImmediately`.
- Resume: the player loads state via the stored `AttemptToken`; if the token is missing/invalid, redirect
  to the landing page.

**Out of scope (later slices / deferred)**
- Batch gate / waiting queue → Slice 2.
- Background auto-submit hosted service, tab-switch logging, copy/paste lockdown, rate limiting → Slice 3.
- **Per-question points override**: the 1a snapshot does not capture per-question points, so 1b grades
  using the exam's per-type points only (`McqPoints` / `TrueFalsePoints` / `FillBlankPoints`). Honoring
  `Question.PointsOverride` would require adding a points column to `AttemptQuestion` and setting it at
  snapshot time; deferred and noted.
- **TrueFalse** questions: not authorable in the bank yet, so none appear in snapshots; the grader handles
  the type generically (option-match) but no dedicated TrueFalse UI is built.
- FillBlank synonyms / alternate answers (PRD "Could").

---

## 3. Backend Architecture (Clean Architecture + CQRS/MediatR)

### 3.1 Domain
- **`AttemptAnswer`** — `Id`, `AttemptId`, `AttemptQuestionId` (unique per attempt-question),
  `SelectedOptionId?` (MCQ / TrueFalse — references an `AttemptQuestionOption.Id` in the snapshot),
  `AnswerText?` (FillBlank), `IsFlagged` (bool), `IsCorrect` (bool, set at grading), `AnsweredAtUtc`.
- **`FillBlankAnswerRules.Normalize(string)`** — new static helper: `Trim()` → `ToLowerInvariant()` →
  remove all internal whitespace. (The existing `AnswerPattern` stays the admin-side authoring rule.)

### 3.2 Application (all handlers operate on an attempt resolved from the token's `attempt_id`)
- **`GetAttemptStateQuery(attemptId)`** → `AttemptStateDto`: `status`, `remainingSeconds`,
  `showResultImmediately`, and an ordered list of **sanitized** questions
  (`attemptQuestionId`, `displayOrder`, `type`, `text`, `imageUrl`, options as `{id, text}` with **no**
  `isCorrect`, and **no** `correctAnswerText`), each carrying the candidate's saved
  `selectedOptionId? / answerText? / isFlagged`. If the attempt is expired and still `InProgress`, it is
  lazily auto-submitted first, and the query returns `status = AutoSubmitted`.
- **`SaveAnswerCommand(attemptId, attemptQuestionId, selectedOptionId?, answerText?, isFlagged)`** →
  upserts the `AttemptAnswer`. Rejects if the attempt is not `InProgress`. If expired → lazy auto-submit
  and return a failure indicating the attempt closed. Validates that a provided `selectedOptionId` belongs
  to that question's snapshot options and that `answerText` length ≤ 50.
- **`SubmitAttemptCommand(attemptId)`** → if already submitted, returns the existing result (idempotent);
  else grades, sets `Status = Submitted`, `SubmittedAtUtc`, `Score`, and returns the result.
- **`GetResultQuery(attemptId)`** → `ResultDto` (`score`, `totalPoints`, `passMarkPercentage`, `passed`)
  when `ShowResultImmediately`; otherwise a withheld-result marker.
- **`IAttemptGradingService`** — grades an attempt's answers against its snapshot: a question is correct
  when (MCQ/TrueFalse) the saved `SelectedOptionId` maps to the snapshot option with `IsCorrect = true`,
  or (FillBlank) `Normalize(AnswerText) == CorrectAnswerTextSnapshot`. Points per correct question come
  from the exam's per-type points. `Score` = sum of correct points; `TotalPoints` = sum of all
  per-question points; `Passed` = `Score / TotalPoints * 100 >= PassMarkPercentage`. Unanswered → wrong.
  A shared helper performs **lazy auto-submit** (grade + set `AutoSubmitted`) and is reused by
  `GetAttemptState` and `SaveAnswer` when they detect expiry.

### 3.3 Infrastructure
- EF configuration + migration for `AttemptAnswer` (unique index on `(AttemptId, AttemptQuestionId)`).
- `AttemptGradingService` implementation registered in DI.
- New `DbSet<AttemptAnswer>` on `IApplicationDbContext` + `ApplicationDbContext`.

### 3.4 API — `CandidateAttemptController` (Route `api/exam/{examId}/attempt`)
All actions `[Authorize(AuthenticationSchemes = "AttemptToken")]`; the controller resolves the attempt from
the token's `attempt_id` claim and returns `403` if it does not match / belong to `{examId}`.
- `GET  /state` → `AttemptStateDto`
- `POST /answer` → save an answer/flag; `204` (or `409` if the attempt has closed)
- `POST /submit` → `ResultDto` (or withheld marker)
- `GET  /result` → `ResultDto` (or withheld marker)

The server is the single source of truth for time and correctness; the client is never trusted and never
receives correctness data before submission.

---

## 4. Frontend (Candidate Area)

Replaces `attempt-shell.component` with the real player under the existing `/exam/:examId/attempt` route.

- **`AttemptPlayerComponent`** — on init calls `GET /state` with the stored token; holds the current
  question index, a countdown derived from `remainingSeconds`, and orchestrates auto-save. On timer zero
  (or a server `409`) it submits/redirects to the result. Missing/invalid token → redirect to landing.
- **`QuestionViewComponent`** — renders per `type`: MCQ/TrueFalse as radio options; FillBlank as a text
  input that forces lowercase, strips spaces, and caps at `maxlength=50`. Emits answer changes.
- **`QuestionNavigatorComponent`** — a grid of question chips colored by state
  (answered / unanswered / flagged), each jumping to that question.
- **`SubmitConfirmComponent`** — a confirmation step showing the unanswered count before final submit.
- **`ResultComponent`** — score / total, pass or fail, per `ShowResultImmediately`; otherwise a
  "submitted, results not shown" message.
- **`CandidateAttemptService`** — `state()`, `saveAnswer()`, `submit()`, `result()` (token attached by the
  existing attempt-token interceptor).
- **Auto-save cadence:** MCQ/flag immediate; FillBlank debounced ~1s. Each save reconciles with the
  server's expiry response.

---

## 5. Error Handling & Edge Cases

- **Expired attempt:** any `state`/`answer`/`submit` on an expired `InProgress` attempt triggers lazy
  auto-submit; the client shows the result (or withheld marker) instead of the player.
- **Already submitted:** `submit` is idempotent; `state` on a submitted attempt returns its final status
  and routes to the result.
- **Invalid option / overlong FillBlank:** `SaveAnswer` rejects with a validation error; the client keeps
  the prior value.
- **Token missing/invalid/mismatched:** `401/403` → redirect to the exam landing page.
- **Unanswered questions at submit:** allowed; the confirmation surfaces the count; unanswered grade as
  wrong.

---

## 6. Testing

- **Backend unit:** grading service (all-correct, all-wrong, mixed, pass/fail boundary; FillBlank
  normalization: trailing space, uppercase, internal spaces; unanswered → wrong); `SaveAnswer` validation
  (option must belong to the question; overlong text rejected; expired → auto-submit); `GetAttemptState`
  sanitization (payload carries no `isCorrect` / `correctAnswerText`); submit idempotency; lazy
  auto-submit on an expired attempt.
- **Backend integration (AttemptToken):** start (from 1a) → `state` → `answer` (MCQ + FillBlank) →
  `submit` → `result`; sanitization check on the `state` response; an attempt past `ExpiresAtUtc`
  auto-submits on the next request; `result` respects `ShowResultImmediately`; a token for attempt A cannot
  read attempt B (`403`).
- **Frontend:** player loads and restores saved answers/flags/remaining time; MCQ saves immediately and
  FillBlank debounces; navigator reflects answered/unanswered/flagged; submit confirmation shows the
  unanswered count; result renders both immediate and withheld modes.
- TDD applied where practical.

---

## 7. Open Items / Follow-ups (not blocking 1b)

- Per-question points override → capture points in the snapshot (touches 1a's `StartAttempt`); revisit if
  the admin starts using overrides.
- Queue integration (Slice 2) does not change 1b's endpoints; it gates *entry*, not *taking*.
- Background auto-submit (Slice 3) supplements — does not replace — 1b's lazy auto-submit.
- Exact Arabic result copy for the withheld-result mode finalized during the UI build.
