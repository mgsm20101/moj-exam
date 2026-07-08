import { Component, OnInit, signal, ViewChild } from '@angular/core';
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
  /// Row whose answers/options detail is expanded.
  expandedId = signal<string | null>(null);

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
      .subscribe(questions => this.questions.set(questions));
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
        this.editingQuestion.set(null);
        this.questionForm?.resetForm();
      },
      error: () => (this.errorMessage = 'تعذّر حفظ السؤال — تحقق من صيغة الإجابة أو الاختيارات.')
    });
  }

  startEdit(question: Question): void {
    this.errorMessage = null;
    this.editingQuestion.set(question);
    // Scroll the edit form into view so the admin sees the pre-filled fields.
    setTimeout(() => document.querySelector('.add-question')?.scrollIntoView({ behavior: 'smooth' }));
  }

  cancelEdit(): void {
    this.editingQuestion.set(null);
    this.questionForm?.resetForm();
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
