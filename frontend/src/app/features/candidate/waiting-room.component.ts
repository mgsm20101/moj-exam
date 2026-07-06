import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CandidateExamService, CandidateIdentity } from '../../core/services/candidate-exam.service';
import { AttemptTokenStore } from '../../core/services/attempt-token.store';

const POLL_MS = 20000;

@Component({
  selector: 'app-waiting-room',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './waiting-room.component.html'
})
export class WaitingRoomComponent implements OnInit, OnDestroy {
  examId = '';
  position = 0;
  estimatedMinutes = 0;
  starting = false;
  private identity: CandidateIdentity | null = null;
  private timer?: ReturnType<typeof setInterval>;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: CandidateExamService,
    private readonly tokenStore: AttemptTokenStore
  ) {}

  ngOnInit(): void {
    this.examId = this.route.parent?.snapshot.paramMap.get('examId') ?? '';
    const raw = localStorage.getItem('queue_' + this.examId);
    this.identity = raw ? (JSON.parse(raw) as CandidateIdentity) : null;
    if (!this.identity) {
      this.router.navigate(['../'], { relativeTo: this.route });
      return;
    }
    this.poll();
    this.timer = setInterval(() => this.poll(), POLL_MS);
  }

  ngOnDestroy(): void {
    if (this.timer) { clearInterval(this.timer); }
  }

  private poll(): void {
    if (!this.identity) { return; }
    this.service.queueStatus(this.examId, this.identity.nationalId).subscribe({
      next: s => {
        this.position = s.position;
        this.estimatedMinutes = Math.ceil(s.estimatedWaitSeconds / 60);
        if (s.status === 'Called' || s.status === 'Expired') { this.tryStart(); }
      }
    });
  }

  private tryStart(): void {
    if (this.starting || !this.identity) { return; }
    this.starting = true;
    this.service.start(this.examId, this.identity).subscribe({
      next: res => {
        if (res.outcome === 'Started' && res.attemptToken) {
          if (this.timer) { clearInterval(this.timer); }
          localStorage.removeItem('queue_' + this.examId);
          this.tokenStore.set(this.examId, res.attemptToken);
          this.router.navigate(['attempt'], { relativeTo: this.route.parent });
        } else {
          this.position = res.queuePosition ?? this.position;
          this.starting = false; // re-queued; keep polling
        }
      },
      error: () => { this.starting = false; }
    });
  }
}
