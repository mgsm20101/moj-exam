import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type ResultsFilter = 'all' | 'passed' | 'failed';

export interface ExamResultRow {
  fullName: string;
  nationalId: string;
  mobileNumber: string;
  score: number;
  totalPoints: number;
  scorePercentage: number;
  passed: boolean;
  submittedAtUtc: string | null;
  governorateCode: number;
  tabSwitchCount: number;
  hasActiveRetakeGrant: boolean;
}

export interface ExamResultsSummary {
  totalCandidates: number;
  passedCount: number;
  failedCount: number;
  passRatePercentage: number;
}

export interface ExamResultsReport {
  examId: string;
  examName: string;
  totalPoints: number;
  passMarkPercentage: number;
  passMarkPoints: number;
  filter: string;
  summary: ExamResultsSummary;
  rows: ExamResultRow[];
}

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/reports`;

  constructor(private readonly http: HttpClient) {}

  getExamResults(examId: string, filter: ResultsFilter = 'all'): Observable<ExamResultsReport> {
    const params = new HttpParams().set('filter', filter);
    return this.http.get<ExamResultsReport>(`${this.baseUrl}/exams/${examId}/results`, { params });
  }

  exportExamResults(examId: string, filter: ResultsFilter = 'all'): Observable<Blob> {
    const params = new HttpParams().set('filter', filter);
    return this.http.get(`${this.baseUrl}/exams/${examId}/results/export`, { params, responseType: 'blob' });
  }

  grantRetake(examId: string, nationalId: string): Observable<void> {
    return this.http.post<void>(
      `${environment.apiBaseUrl}/admin/exams/${examId}/candidates/${nationalId}/grant-retake`,
      {}
    );
  }
}
