import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Topic {
  id: string;
  name: string;
  displayOrder: number;
  isActive: boolean;
  questionCount: number;
}

export interface TopicInput {
  name: string;
  displayOrder: number;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class TopicService {
  private readonly baseUrl = `${environment.apiBaseUrl}/admin/topics`;

  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<Topic[]> {
    return this.http.get<Topic[]>(this.baseUrl);
  }

  create(input: TopicInput): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, input);
  }

  update(id: string, input: TopicInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, input);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
