import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type QuestionType = 'Mcq' | 'TrueFalse' | 'FillBlank';
export type Difficulty = 'Easy' | 'Medium' | 'Hard';

export interface QuestionOption {
  id?: string;
  text: string;
  isCorrect: boolean;
  displayOrder?: number;
}

export interface Question {
  id: string;
  topicId: string;
  topicName: string;
  type: QuestionType;
  difficulty: Difficulty;
  text: string;
  imageUrl: string | null;
  correctAnswerText: string | null;
  pointsOverride: number | null;
  isActive: boolean;
  options: QuestionOption[];
}

export interface QuestionInput {
  topicId: string;
  type: QuestionType;
  difficulty: Difficulty;
  text: string;
  imageUrl?: string | null;
  options?: { text: string; isCorrect: boolean }[];
  correctAnswerText?: string | null;
  pointsOverride?: number | null;
}

export interface QuestionFilters {
  topicId?: string;
  difficulty?: Difficulty;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class QuestionService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/questions`;

  constructor(private readonly http: HttpClient) {}

  getAll(filters: QuestionFilters = {}): Observable<Question[]> {
    let params = new HttpParams();
    if (filters.topicId) params = params.set('topicId', filters.topicId);
    if (filters.difficulty) params = params.set('difficulty', filters.difficulty);
    if (filters.isActive !== undefined) params = params.set('isActive', String(filters.isActive));

    return this.http.get<Question[]>(this.baseUrl, { params });
  }

  create(input: QuestionInput): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, input);
  }

  update(id: string, input: QuestionInput & { isActive: boolean }): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, input);
  }

  deactivate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  uploadImage(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/image`, formData);
  }
}
