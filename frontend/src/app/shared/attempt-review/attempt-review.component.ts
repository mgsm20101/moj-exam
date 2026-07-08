import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AttemptReview, AttemptReviewQuestion } from './attempt-review.model';

/**
 * Presentational per-question review: shows every presented question with the correct answer and the
 * candidate's chosen answer highlighted. Reused by the admin report and the candidate result screen.
 */
@Component({
  selector: 'app-attempt-review',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './attempt-review.component.html',
  styleUrl: './attempt-review.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AttemptReviewComponent {
  @Input({ required: true }) review!: AttemptReview;

  /// Admins see which answer is correct; candidates (false) see only whether their own answer was
  /// right or wrong, never the correct answer itself.
  @Input() revealCorrect = true;

  /** Only the questions the candidate got wrong or skipped — used for the "wrong only" toggle. */
  get wrongCount(): number {
    return this.review.questions.filter(q => !q.isCorrect).length;
  }

  showWrongOnly = false;

  get visibleQuestions(): AttemptReviewQuestion[] {
    return this.showWrongOnly
      ? this.review.questions.filter(q => !q.isCorrect)
      : this.review.questions;
  }

  toggleWrongOnly(): void {
    this.showWrongOnly = !this.showWrongOnly;
  }
}
