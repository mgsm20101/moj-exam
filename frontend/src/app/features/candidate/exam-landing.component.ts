import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CandidateExamService, ExamLanding } from '../../core/services/candidate-exam.service';

const FOUR_WORDS = /^\s*\S+(\s+\S+){3,}\s*$/;
const NATIONAL_ID = /^[23]\d{13}$/;
const MOBILE = /^01[0125]\d{8}$/;

@Component({
  selector: 'app-exam-landing',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './exam-landing.component.html'
})
export class ExamLandingComponent implements OnInit {
  examId = '';
  landing: ExamLanding | null = null;
  loading = true;
  submitting = false;
  blockedMessage: string | null = null;

  readonly form = this.fb.group({
    fullName: ['', [Validators.required, Validators.pattern(FOUR_WORDS)]],
    nationalId: ['', [Validators.required, Validators.pattern(NATIONAL_ID)]],
    mobileNumber: ['', [Validators.required, Validators.pattern(MOBILE)]]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: CandidateExamService
  ) {}

  ngOnInit(): void {
    this.examId = this.route.parent?.snapshot.paramMap.get('examId') ?? '';
    this.service.landing(this.examId).subscribe({
      next: l => { this.landing = l; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting) { return; }
    this.submitting = true;
    this.blockedMessage = null;
    const identity = this.form.getRawValue() as { fullName: string; nationalId: string; mobileNumber: string };

    this.service.register(this.examId, identity).subscribe({
      next: res => {
        this.submitting = false;
        if (res.status === 'CanStart') {
          this.router.navigate(['instructions'], { relativeTo: this.route.parent, state: { identity } });
        } else if (res.status === 'AlreadyTaken') {
          this.blockedMessage = 'لقد أدّيت هذا الامتحان من قبل. لا يمكن إعادة الدخول إلا بتفعيل صريح من إدارة النظام.';
        } else {
          this.blockedMessage = 'هذا الامتحان غير متاح حالياً.';
        }
      },
      error: () => {
        this.submitting = false;
        this.blockedMessage = 'تعذّر التحقّق من البيانات — راجع الحقول وحاول مرة أخرى.';
      }
    });
  }
}
