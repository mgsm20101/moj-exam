import { Component, OnInit, computed, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { Difficulty, Question, QuestionInput, QuestionService } from '../../../core/services/question.service';
import { Topic, TopicService } from '../../../core/services/topic.service';
import { QuestionFormComponent } from './question-form.component';

@Component({
  selector: 'app-questions-list',
  standalone: true,
  imports: [CommonModule, FormsModule, QuestionFormComponent],
  templateUrl: './questions-list.component.html'
})
export class QuestionsListComponent implements OnInit {
  @ViewChild(QuestionFormComponent) questionForm?: QuestionFormComponent;

  topics = signal<Topic[]>([]);
  questions = signal<Question[]>([]);
  selectedTopicId = '';
  selectedDifficulty: Difficulty | '' = '';
  errorMessage: string | null = null;

  /// The question currently being edited (null = the form is in "add" mode).
  editingQuestion = signal<Question | null>(null);
  /// Whether the add/edit form is shown in its modal (client note 4 — no more scroll-to-bottom).
  isFormOpen = signal(false);
  /// Row whose answers/options detail is expanded.
  expandedId = signal<string | null>(null);

  // --- Pagination (client note 3) -----------------------------------------
  // The API returns the full filtered list; we page it client-side to keep the bank scannable.
  readonly pageSize = 10;
  currentPage = signal(1);
  totalPages = computed(() => Math.max(1, Math.ceil(this.questions().length / this.pageSize)));
  pagedQuestions = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize;
    return this.questions().slice(start, start + this.pageSize);
  });

  goToPage(page: number): void {
    const clamped = Math.min(Math.max(1, page), this.totalPages());
    this.currentPage.set(clamped);
    this.expandedId.set(null);
  }

  readonly typeLabels: Record<string, string> = {
    Mcq: 'اختيار من متعدد',
    FillBlank: 'أكمل الناقص',
    TrueFalse: 'صح / خطأ'
  };
  readonly difficultyLabels: Record<string, string> = {
    Easy: 'سهل',
    Medium: 'متوسط',
    Hard: 'متقدم'
  };
  readonly difficultyBadge: Record<string, string> = {
    Easy: 'badge-active',
    Medium: 'badge-info',
    Hard: 'badge-warning'
  };

  constructor(
    private readonly questionService: QuestionService,
    private readonly topicService: TopicService
  ) {}

  ngOnInit(): void {
    this.topicService.getAll().subscribe(topics => this.topics.set(topics));
    this.applyFilters();
  }

  applyFilters(): void {
    this.questionService
      .getAll({
        topicId: this.selectedTopicId || undefined,
        difficulty: this.selectedDifficulty || undefined
      })
      .subscribe(questions => {
        this.questions.set(questions);
        this.currentPage.set(1);
      });
  }

  onQuestionSave(input: QuestionInput): void {
    this.errorMessage = null;
    const editing = this.editingQuestion();

    const request$: Observable<unknown> = editing
      ? this.questionService.update(editing.id, { ...input, isActive: editing.isActive })
      : this.questionService.create(input);

    request$.subscribe({
      next: () => {
        this.applyFilters();
        this.closeForm();
      },
      error: () => (this.errorMessage = 'تعذّر حفظ السؤال — تحقق من صيغة الإجابة أو الاختيارات.')
    });
  }

  /// Opens the modal in "add" mode (client note 4).
  openCreateForm(): void {
    this.errorMessage = null;
    this.editingQuestion.set(null);
    this.questionForm?.resetForm();
    this.isFormOpen.set(true);
  }

  startEdit(question: Question): void {
    this.errorMessage = null;
    this.editingQuestion.set(question);
    this.isFormOpen.set(true);
  }

  closeForm(): void {
    this.isFormOpen.set(false);
    this.editingQuestion.set(null);
    this.questionForm?.resetForm();
  }

  cancelEdit(): void {
    this.closeForm();
  }

  toggleExpand(id: string): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  onImageFileSelected(file: File): void {
    this.errorMessage = null;
    this.questionService.uploadImage(file).subscribe({
      next: res => this.questionForm?.setImageUrl(res.url),
      error: () => (this.errorMessage = 'تعذّر رفع الصورة.')
    });
  }

  deactivateQuestion(id: string): void {
    this.errorMessage = null;
    this.questionService.deactivate(id).subscribe({
      next: () => this.applyFilters(),
      error: () => (this.errorMessage = 'تعذّر تعطيل السؤال.')
    });
  }
}
