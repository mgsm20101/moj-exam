import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ExamLanding {
  examId: string;
  name: string;
  description: string | null;
  isOpen: boolean;
  durationMinutes: number;
  totalQuestionCount: number;
}

export type RegisterStatus = 'CanStart' | 'AlreadyTaken' | 'NotOpen';

export interface CandidateIdentity {
  fullName: string;
  nationalId: string;
  mobileNumber: string;
}

export interface RegisterResult { status: RegisterStatus; candidateId: string; }
export interface StartAttemptResult { attemptId: string; attemptToken: string; expiresAtUtc: string; }

@Injectable({ providedIn: 'root' })
export class CandidateExamService {
  private readonly baseUrl = `${environment.apiBaseUrl}/exam`;

  constructor(private readonly http: HttpClient) {}

  landing(examId: string): Observable<ExamLanding> {
    return this.http.get<ExamLanding>(`${this.baseUrl}/${examId}/landing`);
  }

  register(examId: string, identity: CandidateIdentity): Observable<RegisterResult> {
    return this.http.post<RegisterResult>(`${this.baseUrl}/${examId}/register`, identity);
  }

  start(examId: string, identity: CandidateIdentity): Observable<StartAttemptResult> {
    return this.http.post<StartAttemptResult>(`${this.baseUrl}/${examId}/start`, identity);
  }
}
