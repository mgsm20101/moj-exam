# Admin Results Reporting — Design (FR-6, first slice)

**Date:** 2026-07-06
**Status:** Approved (approach A)
**Depends on:** Candidate exam-taking + grading (slices 1a/1b/2/3, all on `master`), Exam configuration (Phase 1b).

## Problem

The admin needs a per-exam results report answering "who passed and how many passed", viewable
on screen and exportable to an Excel sheet. This is the only unbuilt piece of the end-to-end flow —
question bank import, exam configuration (25 MCQ + 5 FillBlank / 75 marks / timed), attempt-taking,
and grading already exist. There is currently no `Features/Reports` area and the existing ClosedXML
usage is read-only (question import); nothing writes `.xlsx`.

## Goal

For a chosen exam, show every candidate's result (name, national ID, mobile, score /75, pass/fail,
submission time) with summary statistics (total candidates, passed, failed, pass rate), filterable by
All / Passed / Failed, and a one-click Excel export that honours the active filter.

## Non-goals (this slice)

- Live monitoring / in-progress dashboards (later FR-5/FR-6 slices).
- Per-question analytics, difficulty breakdowns, tab-switch/integrity dashboards.
- Cross-exam aggregate reporting.
- Scheduled/emailed reports.

## Chosen approach — A: read-model query + ClosedXML exporter

A CQRS **query** computes the report on the fly from `ExamAttempt` + `Candidate` + `Exam`; a new
`IExcelReportWriter` interface (Application) with a `ClosedXmlReportWriter` implementation
(Infrastructure) writes the workbook — mirroring the existing `IExcelQuestionParser` /
`ClosedXmlQuestionParser` pair exactly. **No new tables, no migration.**

Rejected: (B) denormalizing `IsPassed`/`TotalMarks` onto `ExamAttempt` — needs a migration + backfill
+ risks drift from the grading formula, and the data volumes are small (YAGNI). (C) computing
stats/Excel in the Angular client — duplicates the pass/fail rule on the client and can't reuse
ClosedXML (rejected on consistency/security grounds).

## Key correctness decisions

- **Pass/fail rule = exactly the grading rule.** From `AttemptGradingService.Grade`:
  `passed = total > 0 && (score / total * 100) >= exam.PassMarkPercentage`. The report reuses this
  identical formula so the report can never disagree with the result the candidate saw.
- **`total` (max marks) is constant per exam.** Exam configuration is immutable once `Published`
  (only `Draft` is editable), so every attempt of an exam shares the same total =
  `Σ TopicSelection.Count × pointsFor(type)` (2/1/5 for MCQ/TrueFalse/FillBlank). The report computes
  this once per exam from `TopicSelections` (same formula as `GetExamsQueryHandler.TotalPoints`),
  avoiding a per-attempt snapshot load. `Score` is stored on the attempt; `passed` is derived.
- **Completed attempts only.** Rows are built from attempts with status `Submitted` or
  `AutoSubmitted`. `InProgress` and `Terminated` attempts are excluded.
- **One row per candidate = their best-scoring completed attempt.** `MaxAttempts` defaults to 1, but
  if a candidate has several completed attempts the highest `Score` wins (ties broken by latest
  `SubmittedAtUtc`). "Number of passers" therefore counts distinct candidates.
- **Summary always reflects all candidates**, independent of the row filter, so the headline numbers
  stay stable while the operator filters the table. The filter affects table rows and the Excel export
  body only.

## Architecture

### Backend (Clean Architecture + CQRS)

`src/ExamSystem.Application/Features/Reports/GetExamResultsReport/`
- `ResultsFilter.cs` — `enum ResultsFilter { All = 0, Passed = 1, Failed = 2 }`.
- `ExamResultRow.cs` — `record ExamResultRow(string FullName, string NationalId, string MobileNumber,
  decimal Score, decimal TotalPoints, decimal ScorePercentage, bool Passed, DateTime? SubmittedAtUtc,
  int GovernorateCode, int TabSwitchCount)`.
- `ExamResultsSummary.cs` — `record ExamResultsSummary(int TotalCandidates, int PassedCount,
  int FailedCount, decimal PassRatePercentage)`.
- `ExamResultsReportDto.cs` — `record ExamResultsReportDto(Guid ExamId, string ExamName,
  decimal TotalPoints, decimal PassMarkPercentage, decimal PassMarkPoints, ResultsFilter Filter,
  ExamResultsSummary Summary, List<ExamResultRow> Rows)`.
- `GetExamResultsReportQuery.cs` — `record GetExamResultsReportQuery(Guid ExamId, ResultsFilter Filter)
  : IRequest<Result<ExamResultsReportDto>>`.
- `GetExamResultsReportQueryHandler.cs` — loads the exam (+ TopicSelections), computes `totalPoints`
  and `passMarkPoints`, loads completed attempts for the exam, reduces to best-per-candidate, joins
  candidates, builds rows + summary, applies the row filter, returns `Result.Failure("Exam not
  found.")` when the exam is missing.

`src/ExamSystem.Application/Common/Interfaces/IExcelReportWriter.cs`
- `byte[] WriteExamResults(ExamResultsReportDto report)`.

`src/ExamSystem.Infrastructure/Files/ClosedXmlReportWriter.cs`
- Builds an RTL workbook: a **Summary** sheet (exam name, total marks, pass mark %, pass mark points,
  totals/passed/failed/pass-rate) and a **Results** sheet (Arabic headers, one row per candidate,
  a bold header row, auto-fit columns). Returns the workbook as a `byte[]`.
- Registered in `Infrastructure/DependencyInjection.cs`:
  `services.AddScoped<IExcelReportWriter, ClosedXmlReportWriter>();`.

`src/ExamSystem.Api/Controllers/ReportsController.cs` — `[ApiController]`,
`[Route("api/admin/reports")]`, `[Authorize(Roles = "Admin")]`:
- `GET exams/{examId}/results?filter=all|passed|failed` → `200` JSON `ExamResultsReportDto`,
  `404` when the exam is missing.
- `GET exams/{examId}/results/export?filter=...` → `200` `FileContentResult`
  (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`),
  download name `{ExamName} - Results.xlsx`; `404` when the exam is missing.
- `filter` query string is parsed case-insensitively to `ResultsFilter`, defaulting to `All`.

### Frontend (Angular 17 standalone, RTL)

`frontend/src/app/core/services/report.service.ts`
- Types `ResultsFilter = 'all' | 'passed' | 'failed'`, `ExamResultRow`, `ExamResultsSummary`,
  `ExamResultsReport`.
- `getExamResults(examId, filter)` → `Observable<ExamResultsReport>`.
- `exportExamResults(examId, filter)` → `Observable<Blob>` (`responseType: 'blob'`).

`frontend/src/app/features/admin/reports/exam-results-report.component.{ts,html}`
- Exam picker (reuses `ExamService.getAll()`), four stat cards (total / passed / failed / pass-rate),
  filter chips (الكل / ناجحون / راسبون), a results table, and a **تصدير Excel** button that downloads
  the blob (creates an object URL + anchor click, honouring the active filter). Uses signals + OnPush.

`frontend/src/app/layouts/admin-layout/admin-layout.component.ts` — add a `التقارير` nav link to
`/admin/reports`.

`frontend/src/app/app.routes.ts` — add a lazy `reports` child route under `admin`.

## Testing (TDD, per repo convention)

- **`GetExamResultsReportQueryHandlerTests`** (Application.UnitTests, EF InMemory via
  `TestDbContextFactory`):
  - classifies pass/fail using the exact grading formula (e.g. 60% pass mark on /75);
  - summary counts total/passed/failed and pass rate over all candidates;
  - `Passed` / `Failed` filters restrict rows but leave the summary unchanged;
  - best-attempt-per-candidate reduction (two attempts, higher score wins);
  - excludes `InProgress` / `Terminated` attempts;
  - unknown exam → failure.
- **`ReportsControllerTests`** (Api.IntegrationTests, SQLite): unauthenticated → `401`; a seeded
  exam+candidate+attempt returns the report JSON; the export endpoint returns `200` with the xlsx
  content type and a non-empty body; unknown exam → `404`.
- **Frontend spec** for `report.service.ts` (HttpTestingController) asserting the URLs, the `filter`
  query param, and `responseType: 'blob'` on export.

## Security (OWASP, per project rules)

- Both endpoints require `[Authorize(Roles = "Admin")]` — no anonymous access to candidate PII.
- `examId` is a route GUID; `filter` is an allow-listed enum parse (no free-form input reaches a query).
- No SQL string building — EF Core parameterized queries only.
- Export is generated server-side; the client never receives the pass/fail rule or unfiltered raw data
  beyond what the report already returns.
