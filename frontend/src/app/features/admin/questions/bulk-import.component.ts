import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BulkImportReport, QuestionBankSummaryRow, QuestionService } from '../../../core/services/question.service';

@Component({
  selector: 'app-bulk-import',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './bulk-import.component.html'
})
export class BulkImportComponent implements OnInit {
  summary = signal<QuestionBankSummaryRow[]>([]);
  report = signal<BulkImportReport | null>(null);
  selectedFile: File | null = null;
  uploading = false;
  errorMessage: string | null = null;

  constructor(private readonly questionService: QuestionService) {}

  ngOnInit(): void {
    this.loadSummary();
  }

  loadSummary(): void {
    this.questionService.getSummary().subscribe({
      next: summary => this.summary.set(summary),
      error: () => (this.errorMessage = 'تعذّر تحميل ملخص البنك.')
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files && input.files.length > 0 ? input.files[0] : null;
  }

  upload(): void {
    if (!this.selectedFile) {
      return;
    }
    this.uploading = true;
    this.errorMessage = null;
    this.questionService.bulkImport(this.selectedFile).subscribe({
      next: report => {
        this.report.set(report);
        this.uploading = false;
        this.loadSummary();
      },
      error: () => {
        this.uploading = false;
        this.errorMessage = 'تعذّر استيراد الملف — تحقق من صيغة الملف والاتصال بالخادم.';
      }
    });
  }
}
