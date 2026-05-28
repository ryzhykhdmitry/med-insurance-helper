import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Citation {
  documentId: string;
  content: string;
  fileName: string;
  pageNumber: number | null;
  chunkIndex: number;
  score: number;
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  citations?: Citation[];
  sections?: ResponseSection[];
  isLoading?: boolean;
}

export interface ResponseSection {
  type: 'comparison' | 'recommendation' | 'answer' | 'clarification' | string;
  content: string;
  payload?: unknown;
}

export interface ResponseArtifact {
  sessionId: string;
  sections: ResponseSection[];
  errors: string[];
}

// New Foundry RAG response format
export interface ChatApiResponse {
  sessionId: string;
  message: string;
  sourceCitations: Citation[];
  timestamp: string;
  expiresAt: string;
}

// Legacy response format
export interface ChatApiResponseLegacy {
  sessionId: string;
  responseArtifact: ResponseArtifact;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly baseUrl = 'http://localhost:5000/api';

  constructor(private http: HttpClient) {}

  /**
   * Sends a message to POST /api/chat and returns the structured response.
   */
  sendMessage(text: string, sessionId?: string): Observable<ChatApiResponse> {
    const body = {
      sessionId: sessionId ?? null,
      message: { text }
    };
    return this.http.post<ChatApiResponse>(`${this.baseUrl}/chat`, body);
  }
}
