import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

export interface Citation {
  documentId: string;
  chunkId: string;
  pageRef: string;
  excerpt: string;
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  citations?: Citation[];
  isStreaming?: boolean;
}

export interface StreamEvent {
  type: 'token' | 'citations' | 'done' | 'error';
  data: string;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly baseUrl = 'http://localhost:5000/api';

  /**
   * Opens an SSE connection to /api/stream and emits parsed events.
   * Caller is responsible for closing the returned EventSource via the teardown Subject.
   */
  streamResponse(
    query: string,
    topK = 5,
    sessionId?: string
  ): { events$: Observable<StreamEvent>; close: () => void } {
    const subject = new Subject<StreamEvent>();
    let params = `query=${encodeURIComponent(query)}&topK=${topK}`;
    if (sessionId) params += `&sessionId=${encodeURIComponent(sessionId)}`;

    const es = new EventSource(`${this.baseUrl}/stream?${params}`);

    es.addEventListener('token', (e: MessageEvent) => {
      subject.next({ type: 'token', data: (e as MessageEvent).data });
    });

    es.addEventListener('citations', (e: MessageEvent) => {
      subject.next({ type: 'citations', data: (e as MessageEvent).data });
    });

    es.addEventListener('done', () => {
      subject.next({ type: 'done', data: '' });
      subject.complete();
      es.close();
    });

    es.onerror = () => {
      subject.next({ type: 'error', data: 'Stream connection error.' });
      subject.complete();
      es.close();
    };

    return {
      events$: subject.asObservable(),
      close: () => {
        es.close();
        subject.complete();
      }
    };
  }
}
