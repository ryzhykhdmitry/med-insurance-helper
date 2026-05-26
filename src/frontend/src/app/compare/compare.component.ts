import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface CompareResponse {
  comparison: { [aspect: string]: { [offerId: string]: string | null } };
}

@Component({
  selector: 'app-compare',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './compare.component.html',
  styleUrl: './compare.component.css'
})
export class CompareComponent {
  offerIdsInput = '';    // comma-separated offer IDs
  aspectsInput = '';     // comma-separated aspects
  comparison: CompareResponse['comparison'] | null = null;
  aspects: string[] = [];
  offerIds: string[] = [];
  isLoading = false;
  error: string | null = null;

  private readonly baseUrl = 'http://localhost:5000/api';

  constructor(private http: HttpClient) {}

  compare(): void {
    const ids = this.offerIdsInput.split(',').map(s => s.trim()).filter(Boolean);
    const aspects = this.aspectsInput.split(',').map(s => s.trim()).filter(Boolean);

    if (ids.length < 2 || aspects.length === 0) {
      this.error = 'Please provide at least 2 offer IDs and at least 1 aspect.';
      return;
    }

    this.isLoading = true;
    this.error = null;
    this.comparison = null;

    this.http.post<CompareResponse>(`${this.baseUrl}/compare`, {
      offerIds: ids,
      aspects
    }).subscribe({
      next: res => {
        this.comparison = res.comparison;
        this.offerIds = ids;
        this.aspects = aspects;
        this.isLoading = false;
      },
      error: err => {
        this.error = err.error?.message ?? 'Comparison failed.';
        this.isLoading = false;
      }
    });
  }

  objectKeys(obj: object): string[] {
    return Object.keys(obj);
  }
}
