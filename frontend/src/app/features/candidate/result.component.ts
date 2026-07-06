import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { AttemptResult } from '../../core/services/candidate-attempt.service';

@Component({
  selector: 'app-result',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="candidate-card">
      <div class="candidate-header"><div class="exam-title">نتيجة الامتحان</div></div>
      <div class="candidate-state" *ngIf="result">
        <ng-container *ngIf="result.shown; else withheld">
          <p class="result-score">{{ result.score }} / {{ result.totalPoints }}</p>
          <p [class.result-pass]="result.passed" [class.result-fail]="!result.passed">
            {{ result.passed ? 'ناجح' : 'راسب' }} (درجة النجاح {{ result.passMarkPercentage }}%)
          </p>
        </ng-container>
        <ng-template #withheld>
          <p>تم تسليم امتحانك بنجاح. ستُعلن النتيجة لاحقاً.</p>
        </ng-template>
      </div>
    </div>
  `
})
export class ResultComponent {
  @Input({ required: true }) result!: AttemptResult;
}
