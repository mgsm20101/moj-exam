import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { AttemptResult, CandidateAttemptService } from '../../core/services/candidate-attempt.service';
import { AttemptReview } from '../../shared/attempt-review/attempt-review.model';
import { AttemptReviewComponent } from '../../shared/attempt-review/attempt-review.component';

@Component({
  selector: 'app-result',
  standalone: true,
  imports: [CommonModule, AttemptReviewComponent],
  template: `
    <div class="candidate-card">
      <div class="candidate-header"><div class="exam-title">نتيجة الامتحان</div></div>
      <div class="candidate-state" *ngIf="result">
        <ng-container *ngIf="result.shown; else withheld">
          <p class="result-score">{{ result.score }} / {{ result.totalPoints }}</p>
          <p [class.result-pass]="result.passed" [class.result-fail]="!result.passed">
            {{ result.passed ? 'ناجح' : 'راسب' }} (درجة النجاح {{ result.passMarkPercentage }}%)
          </p>

          <button
            type="button"
            class="btn-ghost"
            *ngIf="!review && examId"
            [disabled]="reviewLoading"
            (click)="loadReview()">
            {{ reviewLoading ? 'جارٍ التحميل…' : 'مراجعة الأسئلة' }}
          </button>
          <p class="result-fail" *ngIf="reviewError">{{ reviewError }}</p>
        </ng-container>
        <ng-template #withheld>
          <p>تم تسليم امتحانك بنجاح. ستُعلن النتيجة لاحقاً.</p>
        </ng-template>
      </div>

      <div class="candidate-review" *ngIf="review">
        <app-attempt-review [review]="review" [revealCorrect]="false"></app-attempt-review>
      </div>
    </div>
  `
})
export class ResultComponent {
  @Input({ required: true }) result!: AttemptResult;
  /// When set, enables the on-demand "review the questions" action for the candidate.
  @Input() examId: string | null = null;

  review: AttemptReview | null = null;
  reviewLoading = false;
  reviewError: string | null = null;

  constructor(private readonly service: CandidateAttemptService) {}

  loadReview(): void {
    if (!this.examId || this.reviewLoading) {
      return;
    }
    this.reviewLoading = true;
    this.reviewError = null;
    this.service.review(this.examId).subscribe({
      next: review => {
        this.reviewLoading = false;
        // The endpoint honours ShowResultImmediately; withheld reviews come back with shown=false.
        this.review = review.shown ? review : null;
        if (!review.shown) {
          this.reviewError = 'المراجعة غير متاحة لهذا الامتحان.';
        }
      },
      error: () => {
        this.reviewLoading = false;
        this.reviewError = 'تعذّر تحميل المراجعة.';
      }
    });
  }
}
