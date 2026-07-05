import { Component, OnInit, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
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
    this.questionService.create(input).subscribe({
      next: () => {
        this.applyFilters();
        this.questionForm?.resetForm();
      },
      error: () => (this.errorMessage = 'تعذّر حفظ السؤال — تحقق من صيغة الإجابة أو الاختيارات.')
    });
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
