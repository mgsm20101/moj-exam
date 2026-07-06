import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { AttemptQuestionState } from '../../core/services/candidate-attempt.service';

@Component({
  selector: 'app-question-navigator',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="q-nav">
      <button type="button" class="q-chip"
              *ngFor="let q of questions; let i = index"
              [class.answered]="isAnswered(q)"
              [class.flagged]="q.isFlagged"
              [class.current]="i === currentIndex"
              (click)="jump.emit(i)">
        {{ i + 1 }}
      </button>
    </div>
  `
})
export class QuestionNavigatorComponent {
  @Input({ required: true }) questions!: AttemptQuestionState[];
  @Input() currentIndex = 0;
  @Output() jump = new EventEmitter<number>();

  isAnswered(q: AttemptQuestionState): boolean {
    return !!q.selectedOptionId || !!(q.answerText && q.answerText.length > 0);
  }
}
