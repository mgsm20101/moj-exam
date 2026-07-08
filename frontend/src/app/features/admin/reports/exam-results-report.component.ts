import { ChangeDetectionStrategy, Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ExamService, ExamSummary } from '../../../core/services/exam.service';
import { ExamResultRow, ExamResultsReport, ReportService, ResultsFilter } from '../../../core/services/report.service';
import { AttemptReview } from '../../../shared/attempt-review/attempt-review.model';
import { AttemptReviewComponent } from '../../../shared/attempt-review/attempt-review.component';

type SortColumn = 'fullName' | 'nationalId' | 'score';

@Component({
  selector: 'app-exam-results-report',
  standalone: true,
  imports: [CommonModule, FormsModule, AttemptReviewComponent],
  templateUrl: './exam-results-report.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExamResultsReportComponent implements OnInit {
  /// Only exams that could have attempts — a Draft has never been taken.
  reportableExams = computed(() => this.exams().filter(e => e.status !== 'Draft'));

  exams = signal<ExamSummary[]>([]);
  selectedExamId = signal<string | null>(null);
  report = signal<ExamResultsReport | null>(null);
  filter = signal<ResultsFilter>('all');
  loading = signal(false);
  exporting = signal(false);
  grantingNationalId = signal<string | null>(null);
  errorMessage: string | null = null;

  // Feature: client-side name/id/mobile search layered on top of the server pass/fail filter.
  nameQuery = signal('');
  filteredRows = computed<ExamResultRow[]>(() => {
    const current = this.report();
    if (!current) {
      return [];
    }
    const query = this.nameQuery().trim().toLowerCase();
    if (!query) {
      return current.rows;
    }
    return current.rows.filter(row =>
      row.fullName.toLowerCase().includes(query) ||
      row.nationalId.toLowerCase().includes(query) ||
      row.mobileNumber.toLowerCase().includes(query));
  });

  // Feature: reorder the table by name / national id / score (defaults to score, high → low, which
  // matches the server's default ordering). Sorting is layered on top of the search filter.
  sortColumn = signal<SortColumn>('score');
  sortDir = signal<'asc' | 'desc'>('desc');
  sortedRows = computed<ExamResultRow[]>(() => {
    const rows = [...this.filteredRows()];
    const column = this.sortColumn();
    const direction = this.sortDir() === 'asc' ? 1 : -1;
    rows.sort((a, b) => {
      let cmp: number;
      if (column === 'score') {
        cmp = a.score - b.score;
      } else if (column === 'nationalId') {
        cmp = a.nationalId.localeCompare(b.nationalId, undefined, { numeric: true });
      } else {
        cmp = a.fullName.localeCompare(b.fullName, 'ar');
      }
      // Stable, predictable tie-break by name so equal keys don't jump around between renders.
      if (cmp === 0) {
        cmp = a.fullName.localeCompare(b.fullName, 'ar');
      }
      return cmp * direction;
    });
    return rows;
  });

  setSort(column: SortColumn): void {
    if (this.sortColumn() === column) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
      return;
    }
    this.sortColumn.set(column);
    // Names/ids read best ascending (A→Z, 0→9); scores read best descending (highest first).
    this.sortDir.set(column === 'score' ? 'desc' : 'asc');
  }

  sortIndicator(column: SortColumn): string {
    if (this.sortColumn() !== column) {
      return '';
    }
    return this.sortDir() === 'asc' ? '▲' : '▼';
  }

  // Per-attempt review overlay state.
  review = signal<AttemptReview | null>(null);
  reviewLoading = signal(false);
  reviewError: string | null = null;

  readonly filters: { value: ResultsFilter; label: string }[] = [
    { value: 'all', label: 'الكل' },
    { value: 'passed', label: 'الناجحون' },
    { value: 'failed', label: 'الراسبون' }
  ];

  constructor(
    private readonly examService: ExamService,
    private readonly reportService: ReportService
  ) {}

  ngOnInit(): void {
    this.examService.getAll().subscribe({
      next: exams => this.exams.set(exams),
      error: () => (this.errorMessage = 'تعذّر تحميل قائمة الامتحانات.')
    });
  }

  onExamChange(examId: string): void {
    this.selectedExamId.set(examId || null);
    this.filter.set('all');
    this.nameQuery.set('');
    this.sortColumn.set('score');
    this.sortDir.set('desc');
    this.report.set(null);
    if (examId) {
      this.loadReport();
    }
  }

  openReview(row: ExamResultRow): void {
    this.reviewError = null;
    this.review.set(null);
    this.reviewLoading.set(true);
    this.reportService.getAttemptReview(row.attemptId).subscribe({
      next: review => {
        this.review.set(review);
        this.reviewLoading.set(false);
      },
      error: () => {
        this.reviewLoading.set(false);
        this.reviewError = 'تعذّر تحميل تفاصيل المحاولة.';
      }
    });
  }

  closeReview(): void {
    this.review.set(null);
    this.reviewLoading.set(false);
    this.reviewError = null;
  }

  setFilter(filter: ResultsFilter): void {
    if (this.filter() === filter) {
      return;
    }
    this.filter.set(filter);
    this.loadReport();
  }

  /// Re-runs the current exam+filter load — used by the error banner's retry action, since
  /// re-picking the same <option> does not re-fire (change).
  retry(): void {
    this.loadReport();
  }

  private loadReport(): void {
    const requestedExamId = this.selectedExamId();
    const requestedFilter = this.filter();
    if (!requestedExamId) {
      return;
    }
    this.loading.set(true);
    this.errorMessage = null;
    this.reportService.getExamResults(requestedExamId, requestedFilter).subscribe({
      next: report => {
        // Ignore a stale response whose exam/filter was superseded by a newer selection.
        if (this.selectedExamId() !== requestedExamId || this.filter() !== requestedFilter) {
          return;
        }
        this.report.set(report);
        this.loading.set(false);
      },
      error: () => {
        if (this.selectedExamId() !== requestedExamId || this.filter() !== requestedFilter) {
          return;
        }
        this.loading.set(false);
        this.errorMessage = 'تعذّر تحميل التقرير.';
      }
    });
  }

  exportExcel(): void {
    const examId = this.selectedExamId();
    const report = this.report();
    if (!examId || !report) {
      return;
    }
    this.exporting.set(true);
    this.errorMessage = null;
    this.reportService.exportExamResults(examId, this.filter()).subscribe({
      next: blob => {
        this.downloadBlob(blob, `${report.examName} - النتائج.xlsx`);
        this.exporting.set(false);
      },
      error: () => {
        this.exporting.set(false);
        this.errorMessage = 'تعذّر تصدير ملف Excel.';
      }
    });
  }

  grantRetake(row: ExamResultRow): void {
    const examId = this.selectedExamId();
    if (!examId || row.hasActiveRetakeGrant || this.grantingNationalId()) {
      return;
    }
    this.grantingNationalId.set(row.nationalId);
    this.errorMessage = null;
    this.reportService.grantRetake(examId, row.nationalId).subscribe({
      next: () => {
        this.grantingNationalId.set(null);
        const current = this.report();
        if (!current) {
          return;
        }
        this.report.set({
          ...current,
          rows: current.rows.map(r =>
            r.nationalId === row.nationalId ? { ...r, hasActiveRetakeGrant: true } : r
          )
        });
      },
      error: () => {
        this.grantingNationalId.set(null);
        this.errorMessage = 'تعذّر منح إعادة الامتحان.';
      }
    });
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
  }
}
