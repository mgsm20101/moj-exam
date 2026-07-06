# Design Spec — Candidate Exam, Slice 1a: Entry, Registration & Attempt Start

- **Date:** 2026-07-06
- **Status:** Approved (design), pending implementation plan
- **Feature:** Candidate-facing exam surface (Ministry of Justice — Judicial Information Center)
- **Scope unit:** Slice 1a of the candidate feature (see Decomposition below)

---

## 1. Context & Decomposition

The admin console (topics, question bank, exam configuration, publishing) exists and is styled
("The Official Ledger"). The **candidate-facing exam surface does not exist at all** — no backend
(no candidate/attempt/queue endpoints or entities) and no frontend. This surface has a deliberately
different personality from the admin console (calm, low-distraction, exam-pressure-aware) per
`PRODUCT.md`, and its own visual identity anchored on the Ministry of Justice emblem.

The full candidate feature is too large for one spec. It is decomposed into vertical, independently
shippable slices:

- **Slice 1 — Core exam-taking (happy path)**, split into:
  - **1a (this spec)** — public entry, registration + validation, permanent candidate profile,
    already-took-this-exam guard, instructions screen, and **Start Attempt** (create `ExamAttempt`
    + question snapshot + server-side timer + attempt token). Capacity assumed available (no queue).
  - **1b** — exam engine: render snapshot questions, per-answer auto-save, mid-exam resume, final
    submit, auto-grading, result screen.
- **Slice 2 — Batch Gate / Waiting Room (FR-8)** — `MaxConcurrentAttempts`, FIFO queue, position
  polling, slot release, grace window, admin "open next batch" + live counts.
- **Slice 3 — Reliability & anti-cheat hardening** — background auto-submit hosted service,
  tab-switch logging, disable copy/paste + right-click (best effort), register rate limiting, OTP (optional).

Each slice gets its own spec → plan → implementation cycle. **This document covers 1a only.**

### Source requirements
Grounded in `PRD-Exam-System_2.md`: FR-1 (registration), FR-1.2/1.4 (national-ID & mobile
validation), FR-1.5/1.5.1/1.5.2 (permanent profile, one attempt per candidate×exam, duplicate
message), FR-1.7 (instructions page), FR-1.8 + §6.3 (branded entry page), FR-2.2/2.3/2.5 (random
selection, ordering, snapshot), FR-2.6 (server-side timer), §5.1 (Attempt Token auth), §6.1
(candidate journey). Standard distribution: 30 questions (25 MCQ + 5 FillBlank), Medium/Hard, 75 points.

---

## 2. Boundary of 1a

**In scope**
- Public exam link → branded landing/registration page.
- Registration form: full name (≥4 words), national ID (14 digits + structural validation),
  Egyptian mobile (11 digits, 010/011/012/015).
- Permanent `Candidate` profile keyed by national ID; reused across exams.
- Guard: has this candidate already taken *this* exam? (respect an active re-activation grant).
- Exam availability check (within Start/End window; exam is Published).
- Instructions screen (duration, question count, rules) with "ابدأ الامتحان".
- **Start Attempt**: create `ExamAttempt`, generate `AttemptQuestion(+Option)` snapshot via random
  selection, set server-side `ExpiresAtUtc`, issue an Attempt Token, and route into the exam-player
  shell (a placeholder owned by 1b).
- Resume-to-start: if an `InProgress` attempt already exists for (candidate × exam), re-issue a token
  instead of creating a new attempt.

**Out of scope (later slices)**
- Answering, auto-save, mid-exam resume, submit, grading, result rendering → **1b**.
- Capacity limiting / waiting queue → **Slice 2** (1a assumes a slot is available).
- Background auto-submit job, tab-switch logging, copy/paste lockdown, rate limiting, OTP → **Slice 3**.

**Simplifying assumptions in 1a**
- Capacity is always available (queue deferred).
- Timer safety net is **lazy** only (checked on each request in 1b); no background job yet.

---

## 3. Backend Architecture (Clean Architecture + CQRS/MediatR)

Follows the existing pattern (`Domain` / `Application` / `Infrastructure` / `Api`, MediatR handlers,
FluentValidation, EF Core, `Result`-style outcomes).

### 3.1 Domain
- **`Candidate`** — `Id`, `NationalId` (unique), `FullName`, `MobileNumber`, derived `BirthDateUtc`,
  `Gender`, `GovernorateCode`, `CreatedAtUtc`.
- **`NationalId`** — value object encapsulating structural validation and derivation
  (`C YYMMDD GG NNNN S`: century, birth date, governorate code, serial→gender, check digit).
- **`ExamAttempt`** — `Id`, `ExamId`, `CandidateId`, `StartedAtUtc`, `ExpiresAtUtc`,
  `SubmittedAtUtc?`, `Status` (`InProgress | Submitted | AutoSubmitted | Terminated`), `Score?`,
  `Seed`. (1a only creates `InProgress`; other states are written by 1b/Slice 3.)
- **`AttemptQuestion`** — immutable snapshot: `Id`, `AttemptId`, `SourceQuestionId`, `TopicId`,
  `DisplayOrder`, `Type`, `Difficulty`, `TextSnapshot`, `ImageUrlSnapshot?`,
  `CorrectAnswerTextSnapshot?` (FillBlank).
- **`AttemptQuestionOption`** — `Id`, `AttemptQuestionId`, `TextSnapshot`, `IsCorrect`, `DisplayOrder`.
- Reads existing **`CandidateExamAttemptGrant`** (re-activation) for the duplicate guard — introduced
  as an entity here since it does not yet exist; only read in 1a, written by admin re-activation later.

Snapshot immutability (FR-2.5) guarantees review integrity even if the bank question changes later.

### 3.2 Application
- **`GetExamLandingQuery`** → exam title, org identity block, `isOpen` (Published + within window),
  and instructions meta (duration minutes, total question count, rules text).
- **`RegisterCandidateForExamCommand`** → validate inputs; find-or-create `Candidate` by national ID;
  determine outcome: `CanStart | AlreadyTaken | NotOpen`. Does **not** create an attempt.
- **`StartAttemptCommand`** → (idempotent) if an `InProgress` attempt exists → return it; else create
  `ExamAttempt`, generate the snapshot via `IQuestionSelectionService`, compute
  `ExpiresAtUtc = StartedAtUtc + Exam.DurationMinutes`, persist, and return
  `{ attemptId, expiresAtUtc }` plus a freshly issued attempt token.
- **Validators** (FluentValidation): name ≥4 words; national ID 14 digits + structural validity;
  mobile 11 digits with a valid prefix.
- **`IQuestionSelectionService`** — deterministic random selection seeded by `attemptId`: for each
  topic (by `DisplayOrder`), for each difficulty in `[Easy, Medium, Hard]`, `RandomSample(pool,
  requiredCount)` then shuffle within the level; append in order. Guarded by the exam's publish-time
  bank-sufficiency validation (already enforced by FR-4.9); if a pool is somehow short at start time,
  fail with a clear error rather than a partial exam.

### 3.3 Infrastructure
- EF Core entity configurations + one migration (SQL Server; SQLite path stays for tests).
- Attempt-token generator: JWT with claims `attemptId`, `candidateId`, `examId`; lifetime = exam end
  + grace margin; signed with a dedicated `AttemptToken` signing key (config/user-secrets), distinct
  from the admin key.
- National-ID governorate lookup table (code → name) for derivation/display.

### 3.4 API — `CandidateExamController` (public area, `/api/exam`)
- `GET  /api/exam/{examId}/landing` — anonymous. Returns branding + `isOpen` + instructions meta.
- `POST /api/exam/{examId}/register` — anonymous. Body: `{ fullName, nationalId, mobileNumber }`.
  Returns `{ status: "CanStart" | "AlreadyTaken" | "NotOpen", candidateRef }`.
- `POST /api/exam/{examId}/start` — anonymous (re-validates identity in body). Returns
  `{ attemptToken, attemptId, expiresAtUtc }`. Subsequent 1b endpoints require the `AttemptToken` scheme.

The server is the source of truth for time (`ExpiresAtUtc`) and for every validation; the client is
never trusted.

---

## 4. Candidate Authentication — Attempt Token

- A JWT distinct from the admin Identity JWT, guarded by a separate authentication scheme/policy
  named `AttemptToken` (registered alongside the existing `JwtBearer` admin scheme).
- Claims: `attemptId`, `candidateId`, `examId`. Expiry = exam `EndAtUtc` + grace margin.
- `landing` / `register` / `start` are anonymous (public entry). 1b's answer/submit/result endpoints
  require a valid `AttemptToken` whose `attemptId` matches the targeted attempt.
- **Client storage:** `localStorage["attempt_{examId}"]`.
- **Resume:** re-registering with the same national ID resolves the existing `InProgress` attempt and
  `start` re-issues a token for it (no duplicate attempt). Full mid-exam state resume is 1b.

---

## 5. Frontend (Candidate Area)

- **Routing:** a public feature area *outside* the admin `authGuard`:
  - `/exam/:examId` — landing + registration (`ExamLandingComponent`)
  - `/exam/:examId/instructions` — instructions + "ابدأ الامتحان" (`InstructionsComponent`)
  - `/exam/:examId/attempt` — exam-player shell (placeholder in 1a; built in 1b)
- **Distinct candidate theme** (separate from the admin Official Ledger): single column, generous
  spacing, larger Cairo type, higher contrast, large touch targets, tablet/mobile first-class, RTL.
  Its own token set layered on the shared base; the admin theme is untouched.
- **Components:**
  - `ExamLandingComponent` — Ministry of Justice branded header (emblem + org lines + exam title) and
    the registration form with live inline validation (national-ID structure as-you-type, mobile
    format, 4-word name). On `CanStart` → navigate to instructions; on `AlreadyTaken`/`NotOpen` →
    show the corresponding informative state.
  - `InstructionsComponent` — duration, question count, rules; "ابدأ الامتحان" calls `start` and
    routes into the player shell.
- **Services:** `CandidateExamService` (`landing`, `register`, `start`) + an HTTP interceptor that
  attaches the Attempt Token to candidate-scoped calls (kept separate from the admin auth interceptor).

### Visual identity
The gold circular Ministry of Justice emblem is the identity anchor. The surface is a calm, light,
formal canvas: soft off-white/paper background, deep ink text at high contrast. Gold is used only as
a thin identity accent (a hairline under the header / the emblem itself), never for text or button
fills (poor contrast). One restrained primary-action color — a **deep judicial green / teal** that
complements the emblem's gold and the scales' laurel — carries "ابدأ الامتحان", distinguishing the
candidate surface from the admin's Authority Blue.

**Required asset:** `frontend/src/assets/moj-logo.png` (the provided emblem) — to be placed in the
repo before/at implementation. Until present, a neutral placeholder renders in its position.

---

## 6. Validation & Error Handling

| Field | Rule | On failure |
|---|---|---|
| Full name | ≥ 4 words | inline: "ادخل الاسم رباعياً" |
| National ID | 14 digits + valid structure (century, real birth date, governorate 01–35/88, check digit) | specific inline message per failing part |
| Mobile | 11 digits, prefix 010/011/012/015 | inline format message |

- **Already took this exam** (no active grant) → blocking screen explaining that a retake requires an
  explicit admin re-activation for this candidate and exam (FR-1.5.2). Exact copy finalized during UI build.
- **Exam not open** (unpublished or outside Start/End window) → informative screen (not an error toast).
- **Insufficient bank at start** (should be prevented by publish-time validation) → clear failure, no
  partial attempt created.
- All rules are enforced server-side regardless of client state.

---

## 7. Testing

- **Backend unit:** `NationalId` validation + derivation (valid/invalid century, bad date, bad
  governorate, gender parity, check digit); `IQuestionSelectionService` (deterministic per seed,
  respects per-topic/difficulty counts, correct ordering, fails on short pool); duplicate-attempt
  guard (blocked vs allowed-with-grant); attempt + snapshot creation and `ExpiresAtUtc` computation.
- **Backend integration:** `landing` (open/closed), `register` (CanStart / AlreadyTaken / NotOpen),
  `start` (creates attempt + snapshot + token; idempotent for an existing InProgress attempt) — using
  the existing SQLite `EnsureCreated` test path.
- **Frontend:** component specs for registration validation states, guarded navigation
  (CanStart→instructions, AlreadyTaken/NotOpen→informative), and `start`→player-shell transition.
- TDD applied where practical (write the failing test first for validators, selection, guard).

---

## 8. Open Items / Follow-ups (not blocking 1a)

- Exact Arabic copy for the duplicate and not-open states (finalized during UI build).
- Real emblem asset committed to `frontend/src/assets/moj-logo.png`.
- `MaxConcurrentAttempts` and the queue integrate in Slice 2 at the `start` seam.
- Background auto-submit, anti-cheat, and rate limiting land in Slice 3.
