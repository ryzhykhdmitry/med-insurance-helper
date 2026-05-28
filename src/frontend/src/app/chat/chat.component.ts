import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
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
  selectedCitation: Citation | null = null;
  sourcesExpanded = new Map<number, boolean>();
  private sub?: Subscription;

  constructor(
    private chatService: ChatService,
    private sessionService: SessionService,
    private sanitizer: DomSanitizer
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
          // Handle new Foundry RAG response format
          this.messages[msgIdx] = {
            role: 'assistant',
            text: response.message,
            citations: response.sourceCitations,
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

  /**
   * Open citation detail modal
   */
  toggleSources(msgIdx: number): void {
    this.sourcesExpanded.set(msgIdx, !this.sourcesExpanded.get(msgIdx));
  }

  isSourcesExpanded(msgIdx: number): boolean {
    return this.sourcesExpanded.get(msgIdx) ?? false;
  }

  showCitation(citation: Citation): void {
    this.selectedCitation = citation;
  }

  /**
   * Close citation detail modal
   */
  closeCitation(): void {
    this.selectedCitation = null;
  }

  /**
   * Get citation number by index (1-based)
   */
  getCitationNumber(msg: ChatMessage, citation: Citation): number {
    return (msg.citations?.indexOf(citation) ?? -1) + 1;
  }

  /**
   * Render message text with clickable citation numbers
   */
  renderMessageWithCitations(text: string, citations?: Citation[]): SafeHtml {
    if (!citations || citations.length === 0) {
      return this.sanitizer.sanitize(1, text) || '';
    }

    // Replace citation markers [1], [2], etc. with clickable spans
    const rendered = text.replace(/\[(\d+)\]/g, (match, num) => {
      const index = parseInt(num, 10) - 1;
      if (index >= 0 && index < citations.length) {
        return `<span class="citation-link" data-citation-index="${index}">${match}</span>`;
      }
      return match;
    });

    return this.sanitizer.bypassSecurityTrustHtml(rendered);
  }

  /**
   * Handle click on citation number in message text
   */
  onCitationClick(event: MouseEvent, msg: ChatMessage): void {
    const target = event.target as HTMLElement;
    if (target.classList.contains('citation-link')) {
      const index = parseInt(target.getAttribute('data-citation-index') || '-1', 10);
      if (index >= 0 && msg.citations && msg.citations[index]) {
        this.showCitation(msg.citations[index]);
      }
    }
  }
}
