import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ChatService, ChatMessage } from './chat.service';
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
    this.sub?.unsubscribe();
  }

  send(): void {
    const query = this.inputText.trim();
    if (!query || this.isLoading) return;

    this.messages.push({ role: 'user', text: query });
    this.inputText = '';
    this.isLoading = true;

    const placeholder: ChatMessage = { role: 'assistant', text: '', isLoading: true };
    this.messages.push(placeholder);
    const msgIdx = this.messages.length - 1;

    this.sub = this.chatService
      .sendMessage(query, this.sessionService.sessionId ?? undefined)
      .subscribe({
        next: response => {
          // Update session ID from server response
          if (response.sessionId) {
            (this.sessionService as any)['_sessionId'] = response.sessionId;
          }
          const artifact = response.responseArtifact;
          const combined = artifact.sections.map(s => s.content).join('\n\n');
          this.messages[msgIdx] = {
            role: 'assistant',
            text: combined,
            sections: artifact.sections,
            isLoading: false
          };
          this.isLoading = false;
        },
        error: () => {
          this.messages[msgIdx] = {
            role: 'assistant',
            text: 'Sorry, something went wrong. Please try again.',
            isLoading: false
          };
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

  sectionLabel(type: string): string {
    switch (type) {
      case 'comparison': return '📊 Comparison';
      case 'recommendation': return '⭐ Recommendation';
      case 'clarification': return '❓ Clarification needed';
      default: return '💬 Answer';
    }
  }
}
