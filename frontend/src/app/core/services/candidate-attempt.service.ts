import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AttemptOption { id: string; text: string; }
export interface AttemptQuestionState {
  attemptQuestionId: string;
  displayOrder: number;
  type: 'Mcq' | 'TrueFalse' | 'FillBlank';
  text: string;
  imageUrl: string | null;
  options: AttemptOption[];
  selectedOptionId: string | null;
  answerText: string | null;
  isFlagged: boolean;
}
export interface AttemptState {
  status: 'InProgress' | 'Submitted' | 'AutoSubmitted' | 'Terminated';
  remainingSeconds: number;
  showResultImmediately: boolean;
  questions: AttemptQuestionState[];
}
export interface SaveAnswerPayload {
  attemptQuestionId: string;
  selectedOptionId: string | null;
  answerText: string | null;
  isFlagged: boolean;
}
export interface AttemptResult {
  shown: boolean;
  score: number;
  totalPoints: number;
  passMarkPercentage: number;
  passed: boolean;
}

@Injectable({ providedIn: 'root' })
export class CandidateAttemptService {
  private base(examId: string): string { return `${environment.apiBaseUrl}/exam/${examId}/attempt`; }

  constructor(private readonly http: HttpClient) {}

  state(examId: string): Observable<AttemptState> {
    return this.http.get<AttemptState>(`${this.base(examId)}/state`);
  }
  saveAnswer(examId: string, payload: SaveAnswerPayload): Observable<void> {
    return this.http.post<void>(`${this.base(examId)}/answer`, payload);
  }
  submit(examId: string): Observable<AttemptResult> {
    return this.http.post<AttemptResult>(`${this.base(examId)}/submit`, {});
  }
  result(examId: string): Observable<AttemptResult> {
    return this.http.get<AttemptResult>(`${this.base(examId)}/result`);
  }

  recordTabSwitch(examId: string): Observable<void> {
    return this.http.post<void>(`${this.base(examId)}/tab-switch`, {});
  }
}
