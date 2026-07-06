import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';
import { ExamDetail, ExamInput, ExamService, ExamSummary } from '../../../core/services/exam.service';
import { Topic, TopicService } from '../../../core/services/topic.service';
import { ExamFormComponent } from './exam-form.component';

@Component({
  selector: 'app-exams-list',
  standalone: true,
  imports: [CommonModule, ExamFormComponent],
  templateUrl: './exams-list.component.html'
})
export class ExamsListComponent implements OnInit {
  topics = signal<Topic[]>([]);
  exams = signal<ExamSummary[]>([]);
  editingExam = signal<ExamDetail | null>(null);
  isFormOpen = signal(false);
  errorMessage: string | null = null;

  private readonly statusMeta: Record<string, { label: string; badge: string } | undefined> = {
    Draft: { label: 'مسودة', badge: 'badge-neutral' },
    Published: { label: 'منشور', badge: 'badge-published' },
    Closed: { label: 'مغلق', badge: 'badge-closed' },
    Archived: { label: 'مؤرشف', badge: 'badge-archived' }
  };

  statusLabel(status: string): string {
    return this.statusMeta[status]?.label ?? status;
  }

  statusBadge(status: string): string {
    return this.statusMeta[status]?.badge ?? 'badge-neutral';
  }

  copiedExamId = signal<string | null>(null);

  /// Copies the public candidate link for an exam to the clipboard, with brief inline feedback.
  /// Uses the async Clipboard API when available, and falls back to a temporary textarea so it
  /// still works without clipboard permissions or in a non-secure context.
  copyExamLink(examId: string): void {
    const url = `${window.location.origin}/exam/${examId}`;
    const done = () => {
      this.copiedExamId.set(examId);
      setTimeout(() => this.copiedExamId.set(null), 2000);
    };

    if (navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(url).then(done).catch(() => this.fallbackCopy(url, done));
    } else {
      this.fallbackCopy(url, done);
    }
  }

  private fallbackCopy(text: string, done: () => void): void {
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.select();
    try {
      document.execCommand('copy');
      done();
    } finally {
      document.body.removeChild(textarea);
    }
  }

  constructor(
    private readonly examService: ExamService,
    private readonly topicService: TopicService
  ) {}

  ngOnInit(): void {
    this.topicService.getAll().subscribe(topics => this.topics.set(topics));
    this.load();
  }

  load(): void {
    this.examService.getAll().subscribe(exams => this.exams.set(exams));
  }

  openCreateForm(): void {
    this.editingExam.set(null);
    this.isFormOpen.set(true);
  }

  openEditForm(id: string): void {
    this.examService.getById(id).subscribe(exam => {
      this.editingExam.set(exam);
      this.isFormOpen.set(true);
    });
  }

  closeForm(): void {
    this.isFormOpen.set(false);
    this.editingExam.set(null);
  }

  onSave(input: ExamInput): void {
    this.errorMessage = null;
    const editing = this.editingExam();
    const request: Observable<unknown> = editing
      ? this.examService.update(editing.id, input)
      : this.examService.create(input);

    request.subscribe({
      next: () => {
        this.closeForm();
        this.load();
      },
      error: () => (this.errorMessage = 'تعذّر حفظ الامتحان — تحقق من الحقول والتوزيع.')
    });
  }

  deleteExam(id: string): void {
    this.errorMessage = null;
    this.examService.delete(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'لا يمكن حذف امتحان تم نشره — قم بأرشفته بدلاً من ذلك.')
    });
  }

  publishExam(id: string): void {
    this.errorMessage = null;
    this.examService.publish(id).subscribe({
      next: () => this.load(),
      error: err => (this.errorMessage = (err.error?.errors ?? ['تعذّر نشر الامتحان.']).join(' ، '))
    });
  }

  closeExam(id: string): void {
    this.errorMessage = null;
    this.examService.close(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'تعذّر إغلاق الامتحان.')
    });
  }

  archiveExam(id: string): void {
    this.errorMessage = null;
    this.examService.archive(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'تعذّر أرشفة الامتحان.')
    });
  }

  cloneExam(id: string): void {
    this.errorMessage = null;
    this.examService.clone(id).subscribe({
      next: () => this.load(),
      error: () => (this.errorMessage = 'تعذّر استنساخ الامتحان.')
    });
  }
}
