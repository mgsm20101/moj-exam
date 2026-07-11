import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type ExamStatus = 'Draft' | 'Published' | 'Closed' | 'Archived';
export type QueueMode = 'Auto' | 'Manual';
export type Difficulty = 'Easy' | 'Medium' | 'Hard';
export type QuestionType = 'Mcq' | 'TrueFalse' | 'FillBlank';

export interface ExamTopicSelectionInput {
  topicId: string;
  displayOrder: number;
  difficulty: Difficulty;
  type: QuestionType;
  count: number;
}

export interface ExamTopicSelectionDto extends ExamTopicSelectionInput {
  topicName: string;
}

export interface ExamInput {
  name: string;
  description: string | null;
  startAtUtc: string;
  endAtUtc: string;
  durationMinutes: number;
  mcqPoints: number;
  trueFalsePoints: number;
  fillBlankPoints: number;
  passMarkPercentage: number;
  maxAttempts: number;
  maxConcurrentAttempts: number;
  graceWindowMinutes: number;
  queueMode: QueueMode;
  shuffleAnswers: boolean;
  showResultImmediately: boolean;
  allowBackNavigation: boolean;
  topicSelections: ExamTopicSelectionInput[];
}

export interface ExamSummary {
  id: string;
  name: string;
  startAtUtc: string;
  endAtUtc: string;
  durationMinutes: number;
  status: ExamStatus;
  queueMode: QueueMode;
  totalQuestionCount: number;
  totalPoints: number;
}

export interface ExamDetail extends ExamInput {
  id: string;
  status: ExamStatus;
  topicSelections: ExamTopicSelectionDto[];
}

export interface ExamLiveCounts {
  examId: string;
  activeAttempts: number;
  maxConcurrentAttempts: number;
  reservedCalled: number;
  waitingCount: number;
}

export interface OpenBatchResult {
  calledCount: number;
  remainingWaiting: number;
  availableAfter: number;
}

@Injectable({ providedIn: 'root' })
export class ExamService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/exams`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<ExamSummary[]> {
    return this.http.get<ExamSummary[]>(this.baseUrl);
  }

  getById(id: string): Observable<ExamDetail> {
    return this.http.get<ExamDetail>(`${this.baseUrl}/${id}`);
  }

  create(input: ExamInput): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, input);
  }

  update(id: string, input: ExamInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, input);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  publish(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/publish`, null);
  }

  close(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/close`, null);
  }

  /** Reopens a Closed exam back to Published (client note 6: "start" it again). */
  reopen(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/reopen`, null);
  }

  archive(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/archive`, null);
  }

  clone(id: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/${id}/clone`, null);
  }

  getLiveCounts(): Observable<ExamLiveCounts[]> {
    return this.http.get<ExamLiveCounts[]>(`${this.baseUrl}/live-counts`);
  }

  setQueueMode(id: string, mode: QueueMode): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/queue-mode`, { mode });
  }

  openNextBatch(id: string, count: number): Observable<OpenBatchResult> {
    return this.http.post<OpenBatchResult>(`${this.baseUrl}/${id}/queue/open-batch`, { count });
  }
}
