import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { tap } from 'rxjs/operators';

export interface SessionResponse {
  sessionId: string;
}

/**
 * Manages the conversation session ID.
 * Persists the sessionId in memory for the lifetime of the app;
 * passes it with every chat request.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly baseUrl = 'http://localhost:5000/api';
  private _sessionId: string | null = null;

  constructor(private http: HttpClient) {}

  get sessionId(): string | null {
    return this._sessionId;
  }

  /**
   * Creates a new session or retrieves the existing one.
   * If a sessionId is already stored in memory, returns it without an HTTP call.
   */
  ensureSession(): Observable<string> {
    if (this._sessionId) {
      return of(this._sessionId);
    }
    return new Observable(observer => {
      this.http
        .post<SessionResponse>(`${this.baseUrl}/session`, {})
        .pipe(tap(r => (this._sessionId = r.sessionId)))
        .subscribe({
          next: r => { observer.next(r.sessionId); observer.complete(); },
          error: err => observer.error(err)
        });
    });
  }

  clearSession(): void {
    this._sessionId = null;
  }
}
