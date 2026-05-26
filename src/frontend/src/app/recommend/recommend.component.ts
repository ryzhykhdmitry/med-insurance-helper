import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface RecommendCitation { offerId: string; page: number; excerpt: string; }
export interface RecommendationItem { offerId: string; reason: string; citations: RecommendCitation[]; }
export interface RecommendResponse { recommendations: RecommendationItem[]; }

@Component({
  selector: 'app-recommend',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './recommend.component.html',
  styleUrl: './recommend.component.css'
})
export class RecommendComponent {
  criteriaInput = '';
  recommendations: RecommendationItem[] = [];
  isLoading = false;
  error: string | null = null;
  private readonly baseUrl = 'http://localhost:5000/api';

  constructor(private http: HttpClient) {}

  recommend(): void {
    if (!this.criteriaInput.trim()) {
      this.error = 'Please describe your needs.';
      return;
    }
    this.isLoading = true;
    this.error = null;
    this.recommendations = [];

    this.http.post<RecommendResponse>(`${this.baseUrl}/recommend`, {
      criteria: this.criteriaInput
    }).subscribe({
      next: res => {
        this.recommendations = res.recommendations;
        this.isLoading = false;
      },
      error: err => {
        this.error = err.error?.message ?? 'Recommendation failed.';
        this.isLoading = false;
      }
    });
  }
}
