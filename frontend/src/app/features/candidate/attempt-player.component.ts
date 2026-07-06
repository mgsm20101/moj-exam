import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  AttemptResult, AttemptState, AttemptQuestionState, CandidateAttemptService
} from '../../core/services/candidate-attempt.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';
import { QuestionViewComponent } from './question-view.component';
import { QuestionNavigatorComponent } from './question-navigator.component';
import { ResultComponent } from './result.component';

@Component({
  selector: 'app-attempt-player',
  standalone: true,
  imports: [CommonModule, QuestionViewComponent, QuestionNavigatorComponent, ResultComponent],
  templateUrl: './attempt-player.component.html'
})
export class AttemptPlayerComponent implements OnInit, OnDestroy {
  examId = '';
  loading = true;
  state: AttemptState | null = null;
  currentIndex = 0;
  remainingSeconds = 0;
  submitting = false;
  confirming = false;
  result: AttemptResult | null = null;

  private timer?: ReturnType<typeof setInterval>;
  private readonly textSave$ = new Subject<AttemptQuestionState>();

  // Best-effort anti-cheat (Slice 3): report tab-switches and block copy/paste/right-click.
  private lastTabSwitchSentAt = 0;
  private readonly onVisibility = () => {
    if (document.hidden && this.state?.status === 'InProgress') {
      const now = Date.now();
      if (now - this.lastTabSwitchSentAt > 3000) {   // throttle a switch storm to one call / 3s
        this.lastTabSwitchSentAt = now;
        this.service.recordTabSwitch(this.examId).subscribe({ error: () => {} });
      }
    }
  };
  private readonly blockEvent = (e: Event) => e.preventDefault();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: CandidateAttemptService,
    private readonly tokenStore: AttemptTokenStore
  ) {}

  get current(): AttemptQuestionState | null { return this.state?.questions[this.currentIndex] ?? null; }
  get unansweredCount(): number {
    return this.state?.questions.filter(q => !q.selectedOptionId && !(q.answerText && q.answerText.length)).length ?? 0;
  }

  ngOnInit(): void {
    this.examId = this.route.parent?.snapshot.paramMap.get('examId') ?? '';
    if (!this.tokenStore.get(this.examId)) {
      this.router.navigate(['../'], { relativeTo: this.route });
      return;
    }
    this.textSave$.pipe(debounceTime(1000)).subscribe(q => this.persist(q));
    this.service.state(this.examId).subscribe({
      next: s => this.applyState(s),
      error: () => this.router.navigate(['../'], { relativeTo: this.route })
    });
  }

  ngOnDestroy(): void {
    if (this.timer) { clearInterval(this.timer); }
    document.removeEventListener('visibilitychange', this.onVisibility);
    document.removeEventListener('contextmenu', this.blockEvent);
    document.removeEventListener('copy', this.blockEvent);
    document.removeEventListener('cut', this.blockEvent);
    document.removeEventListener('paste', this.blockEvent);
  }

  private applyState(s: AttemptState): void {
    this.loading = false;
    this.state = s;
    if (s.status !== 'InProgress') { this.loadResult(); return; }
    this.remainingSeconds = s.remainingSeconds;
    this.startTimer();

    document.addEventListener('visibilitychange', this.onVisibility);
    document.addEventListener('contextmenu', this.blockEvent);
    document.addEventListener('copy', this.blockEvent);
    document.addEventListener('cut', this.blockEvent);
    document.addEventListener('paste', this.blockEvent);
  }

  private startTimer(): void {
    this.timer = setInterval(() => {
      this.remainingSeconds = Math.max(0, this.remainingSeconds - 1);
      if (this.remainingSeconds === 0) { clearInterval(this.timer); this.doSubmit(); }
    }, 1000);
  }

  selectOption(optionId: string): void {
    if (!this.current) { return; }
    this.current.selectedOptionId = optionId;
    this.persist(this.current);
  }

  changeText(): void {
    if (this.current) { this.textSave$.next(this.current); }
  }

  toggleFlag(): void {
    if (!this.current) { return; }
    this.current.isFlagged = !this.current.isFlagged;
    this.persist(this.current);
  }

  private persist(q: AttemptQuestionState): void {
    this.service.saveAnswer(this.examId, {
      attemptQuestionId: q.attemptQuestionId,
      selectedOptionId: q.selectedOptionId,
      answerText: q.answerText,
      isFlagged: q.isFlagged
    }).subscribe({ error: () => this.doSubmit() }); // a 409 means time is up
  }

  go(index: number): void { this.currentIndex = index; }
  prev(): void { if (this.currentIndex > 0) { this.currentIndex--; } }
  next(): void { if (this.state && this.currentIndex < this.state.questions.length - 1) { this.currentIndex++; } }

  askConfirm(): void { this.confirming = true; }
  cancelConfirm(): void { this.confirming = false; }

  doSubmit(): void {
    if (this.submitting) { return; }
    this.submitting = true;
    if (this.timer) { clearInterval(this.timer); }
    this.service.submit(this.examId).subscribe({
      next: r => { this.result = r; this.confirming = false; },
      error: () => this.loadResult()
    });
  }

  private loadResult(): void {
    this.service.result(this.examId).subscribe({ next: r => (this.result = r) });
  }
}
