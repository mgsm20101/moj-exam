import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CandidateExamService, CandidateIdentity, ExamLanding } from '../../core/services/candidate-exam.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';

@Component({
  selector: 'app-instructions',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './instructions.component.html'
})
export class InstructionsComponent implements OnInit {
  examId = '';
  landing: ExamLanding | null = null;
  starting = false;
  error: string | null = null;
  private identity: CandidateIdentity | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: CandidateExamService,
    private readonly tokenStore: AttemptTokenStore
  ) {}

  ngOnInit(): void {
    this.examId = this.route.parent?.snapshot.paramMap.get('examId') ?? '';
    this.identity = (history.state?.identity as CandidateIdentity) ?? null;
    if (!this.identity) {
      this.router.navigate(['../'], { relativeTo: this.route });
      return;
    }
    this.service.landing(this.examId).subscribe({ next: l => (this.landing = l) });
  }

  start(): void {
    if (this.starting || !this.identity) { return; }
    this.starting = true;
    this.error = null;
    this.service.start(this.examId, this.identity).subscribe({
      next: res => {
        this.tokenStore.set(this.examId, res.attemptToken);
        this.router.navigate(['attempt'], { relativeTo: this.route.parent });
      },
      error: () => { this.starting = false; this.error = 'تعذّر بدء الامتحان — حاول مرة أخرى.'; }
    });
  }
}
