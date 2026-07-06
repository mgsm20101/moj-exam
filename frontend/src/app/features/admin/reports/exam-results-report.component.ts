import { ChangeDetectionStrategy, Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExamService, ExamSummary } from '../../../core/services/exam.service';
import { ExamResultsReport, ReportService, ResultsFilter } from '../../../core/services/report.service';

@Component({
  selector: 'app-exam-results-report',
  standalone: true,
  imports: [CommonModule],
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
  errorMessage: string | null = null;

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
    this.report.set(null);
    if (examId) {
      this.loadReport();
    }
  }

  setFilter(filter: ResultsFilter): void {
    if (this.filter() === filter) {
      return;
    }
    this.filter.set(filter);
    this.loadReport();
  }

  private loadReport(): void {
    const examId = this.selectedExamId();
    if (!examId) {
      return;
    }
    this.loading.set(true);
    this.errorMessage = null;
    this.reportService.getExamResults(examId, this.filter()).subscribe({
      next: report => {
        this.report.set(report);
        this.loading.set(false);
      },
      error: () => {
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
