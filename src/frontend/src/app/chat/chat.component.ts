import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ChatService, ChatMessage, Citation } from './chat.service';
import { SessionService } from './session.service';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.css'
})
export class ChatComponent implements OnInit, OnDestroy {
  messages: ChatMessage[] = [];
  inputText = '';
  isLoading = false;
  private streamClose?: () => void;
  private sub?: Subscription;

  constructor(
    private chatService: ChatService,
    private sessionService: SessionService
  ) {}

  ngOnInit(): void {
    this.sessionService.ensureSession().subscribe({
      error: () => console.warn('Session init failed; proceeding without session.')
    });
  }

  ngOnDestroy(): void {
    this.streamClose?.();
    this.sub?.unsubscribe();
  }

  send(): void {
    const query = this.inputText.trim();
    if (!query || this.isLoading) return;

    this.messages.push({ role: 'user', text: query });
    this.inputText = '';
    this.isLoading = true;

    // Create placeholder assistant message for streaming
    const assistantMsg: ChatMessage = { role: 'assistant', text: '', isStreaming: true };
    this.messages.push(assistantMsg);
    const msgIdx = this.messages.length - 1;

    const { events$, close } = this.chatService.streamResponse(
      query, 5, this.sessionService.sessionId ?? undefined
    );
    this.streamClose = close;

    this.sub = events$.subscribe({
      next: event => {
        if (event.type === 'token') {
          this.messages[msgIdx].text += event.data;
        } else if (event.type === 'citations') {
          try {
            this.messages[msgIdx].citations = JSON.parse(event.data) as Citation[];
          } catch { /* ignore parse errors */ }
        } else if (event.type === 'done') {
          this.messages[msgIdx].isStreaming = false;
          this.isLoading = false;
        } else if (event.type === 'error') {
          this.messages[msgIdx].text += '\n[Stream error]';
          this.messages[msgIdx].isStreaming = false;
          this.isLoading = false;
        }
      },
      complete: () => {
        this.messages[msgIdx].isStreaming = false;
        this.isLoading = false;
      }
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  clearChat(): void {
    this.messages = [];
    this.sessionService.clearSession();
    this.sessionService.ensureSession().subscribe();
  }
}
