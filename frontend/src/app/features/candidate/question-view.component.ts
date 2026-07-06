import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { AttemptQuestionState } from '../../core/services/candidate-attempt.service';

@Component({
  selector: 'app-question-view',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './question-view.component.html'
})
export class QuestionViewComponent {
  @Input({ required: true }) question!: AttemptQuestionState;
  @Output() optionSelected = new EventEmitter<string>();
  @Output() textChanged = new EventEmitter<string>();

  onText(value: string): void {
    // FillBlank rule: lowercase, no spaces, max 50 (server enforces too)
    const clean = value.toLowerCase().replace(/\s+/g, '').slice(0, 50);
    this.question.answerText = clean;
    this.textChanged.emit(clean);
  }
}
