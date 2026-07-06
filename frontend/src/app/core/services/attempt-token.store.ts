import { Injectable } from '@angular/core';

/** Stores the per-exam candidate attempt token separately from the admin auth token. */
@Injectable({ providedIn: 'root' })
export class AttemptTokenStore {
  private key(examId: string): string { return `attempt_${examId}`; }

  set(examId: string, token: string): void { localStorage.setItem(this.key(examId), token); }
  get(examId: string): string | null { return localStorage.getItem(this.key(examId)); }
  clear(examId: string): void { localStorage.removeItem(this.key(examId)); }
}
