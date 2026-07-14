import {Component, computed, inject, signal} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormBuilder, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';

import {DownloadProgress, DownloadRequest, DownloadService} from '../core/services/download.service';

@Component({
  selector: 'app-download',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="download-container">
      <div class="download-card">
        <header class="download-header">
          <h1>Download Statements</h1>
          <p>Download your account statements in PDF, CSV, or Excel format</p>
        </header>

        <form [formGroup]="downloadForm" (ngSubmit)="onDownload()" class="download-form">
          <div class="form-row">
            <div class="form-group">
              <label for="year">Year</label>
              <select id="year" formControlName="year">
                <option *ngFor="let year of availableYears()" [value]="year">
                  {{ year }}
                </option>
              </select>
            </div>

            <div class="form-group">
              <label for="month">Month</label>
              <select id="month" formControlName="month">
                <option *ngFor="let month of months(); let i = index" [value]="i + 1">
                  {{ month }}
                </option>
              </select>
            </div>

            <div class="form-group">
              <label for="format">Format</label>
              <select id="format" formControlName="format">
                <option value="pdf">PDF</option>
                <option value="csv">CSV</option>
                <option value="xlsx">Excel</option>
              </select>
            </div>
          </div>

          <div class="form-actions">
            <button
              type="submit"
              class="btn btn-primary"
              [disabled]="downloadForm.invalid || isAnyDownloadInProgress()"
            >
              <span class="icon">📥</span>
              Download Statement
            </button>
          </div>
        </form>

        <!-- Recent Downloads -->
        <div class="recent-downloads" *ngIf="recentDownloads().length > 0">
          <h3>Recent Downloads</h3>
          <div class="downloads-list">
            <div
              class="download-item"
              *ngFor="let download of recentDownloads()"
              [class.downloading]="download.progress.isDownloading"
              [class.error]="download.progress.error"
            >
              <div class="download-info">
                <div class="download-name">
                  {{ getDownloadDisplayName(download.request) }}
                </div>
                <div class="download-meta">
                  {{ download.request.year }} - {{ getMonthName(download.request.month) }}
                  ({{ download.request.format?.toUpperCase() }})
                </div>
              </div>

              <div class="download-status">
                <!-- Progress Bar -->
                <div class="progress-container" *ngIf="download.progress.isDownloading">
                  <div class="progress-bar">
                    <div
                      class="progress-fill"
                      [style.width.%]="download.progress.progress"
                    ></div>
                  </div>
                  <span class="progress-text">{{ download.progress.progress }}%</span>
                </div>

                <!-- Success State -->
                <div class="success-state" *ngIf="!download.progress.isDownloading && !download.progress.error">
                  <span class="success-icon">✅</span>
                  <span>Downloaded</span>
                </div>

                <!-- Error State -->
                <div class="error-state" *ngIf="download.progress.error">
                  <span class="error-icon">❌</span>
                  <span class="error-message">{{ download.progress.error }}</span>
                </div>

                <!-- Retry Count -->
                <div class="retry-info" *ngIf="download.progress.retryCount > 0">
                  <span class="retry-text">Retry {{ download.progress.retryCount }}/3</span>
                </div>
              </div>

              <div class="download-actions">
                <!-- Retry Button -->
                <button
                  class="btn btn-small btn-secondary"
                  *ngIf="download.progress.error"
                  (click)="retryDownload(download.request)"
                >
                  🔄 Retry
                </button>

                <!-- Download Again Button -->
                <button
                  class="btn btn-small btn-secondary"
                  *ngIf="!download.progress.isDownloading && !download.progress.error"
                  (click)="downloadAgain(download.request)"
                >
                  📥 Download Again
                </button>

                <!-- Cancel Button -->
                <button
                  class="btn btn-small btn-danger"
                  *ngIf="download.progress.isDownloading"
                  (click)="cancelDownload(download.request)"
                >
                  ❌ Cancel
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Download Tips -->
        <div class="download-tips">
          <h4>📋 Download Tips</h4>
          <ul>
            <li><strong>PDF:</strong> Best for viewing and printing statements</li>
            <li><strong>CSV:</strong> Import into spreadsheet applications</li>
            <li><strong>Excel:</strong> Advanced analysis with formulas and charts</li>
            <li>Downloads are automatically saved to your browser's download folder</li>
            <li>Large statements may take several seconds to generate</li>
          </ul>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .download-container {
      max-width: 800px;
      margin: 0 auto;
      padding: 20px;
      min-height: 100vh;
      background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
    }

    .download-card {
      background: white;
      border-radius: 16px;
      padding: 32px;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.1);
    }

    .download-header {
      text-align: center;
      margin-bottom: 32px;
    }

    .download-header h1 {
      margin: 0 0 8px 0;
      color: #333;
      font-size: 28px;
    }

    .download-header p {
      margin: 0;
      color: #666;
      font-size: 16px;
    }

    .download-form {
      margin-bottom: 32px;
    }

    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 16px;
      margin-bottom: 24px;
    }

    @media (max-width: 600px) {
      .form-row {
        grid-template-columns: 1fr;
      }
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .form-group label {
      font-weight: 500;
      color: #333;
      font-size: 14px;
    }

    .form-group select {
      padding: 12px;
      border: 2px solid #e0e0e0;
      border-radius: 8px;
      font-size: 16px;
      background: white;
      transition: border-color 0.2s;
    }

    .form-group select:focus {
      outline: none;
      border-color: #667eea;
      box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
    }

    .form-actions {
      text-align: center;
    }

    .btn {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 12px 24px;
      border: none;
      border-radius: 8px;
      font-size: 16px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
      text-decoration: none;
    }

    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-primary {
      background: #667eea;
      color: white;
    }

    .btn-primary:hover:not(:disabled) {
      background: #5a6fd8;
      transform: translateY(-1px);
    }

    .btn-secondary {
      background: #e9ecef;
      color: #495057;
    }

    .btn-secondary:hover:not(:disabled) {
      background: #dee2e6;
    }

    .btn-danger {
      background: #ff6b6b;
      color: white;
    }

    .btn-danger:hover:not(:disabled) {
      background: #ff5252;
    }

    .btn-small {
      padding: 6px 12px;
      font-size: 12px;
    }

    .recent-downloads {
      margin-bottom: 32px;
      padding-top: 24px;
      border-top: 1px solid #e0e0e0;
    }

    .recent-downloads h3 {
      margin: 0 0 16px 0;
      color: #333;
      font-size: 20px;
    }

    .downloads-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .download-item {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 16px;
      background: #f8f9fa;
      border-radius: 8px;
      border-left: 4px solid #e0e0e0;
      transition: all 0.2s;
    }

    .download-item.downloading {
      border-left-color: #ffd43b;
      background: #fffbf0;
    }

    .download-item.error {
      border-left-color: #ff6b6b;
      background: #fff5f5;
    }

    .download-info {
      flex: 1;
    }

    .download-name {
      font-weight: 500;
      color: #333;
      margin-bottom: 4px;
    }

    .download-meta {
      font-size: 14px;
      color: #666;
    }

    .download-status {
      min-width: 200px;
      text-align: center;
    }

    .progress-container {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .progress-bar {
      flex: 1;
      height: 8px;
      background: #e0e0e0;
      border-radius: 4px;
      overflow: hidden;
    }

    .progress-fill {
      height: 100%;
      background: #667eea;
      border-radius: 4px;
      transition: width 0.3s ease;
    }

    .progress-text {
      font-size: 12px;
      color: #666;
      min-width: 35px;
    }

    .success-state {
      display: flex;
      align-items: center;
      gap: 6px;
      color: #51cf66;
      font-size: 14px;
    }

    .error-state {
      display: flex;
      align-items: center;
      gap: 6px;
      color: #ff6b6b;
      font-size: 14px;
    }

    .error-message {
      font-size: 12px;
      max-width: 150px;
      word-wrap: break-word;
    }

    .retry-info {
      margin-top: 4px;
    }

    .retry-text {
      font-size: 11px;
      color: #ffd43b;
      font-weight: 500;
    }

    .download-actions {
      display: flex;
      gap: 8px;
    }

    .download-tips {
      background: #f8f9fa;
      border-radius: 8px;
      padding: 20px;
      border-left: 4px solid #667eea;
    }

    .download-tips h4 {
      margin: 0 0 12px 0;
      color: #333;
      font-size: 16px;
    }

    .download-tips ul {
      margin: 0;
      padding-left: 20px;
      color: #666;
    }

    .download-tips li {
      margin-bottom: 8px;
      line-height: 1.5;
    }

    .download-tips strong {
      color: #333;
    }
  `]
})
export class DownloadComponent {
  private readonly downloadService = inject(DownloadService);
  private readonly fb = inject(FormBuilder);

  downloadForm: FormGroup;

  months = signal([
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ]);

  availableYears = computed(() => {
    const currentYear = new Date().getFullYear();
    const years = [];
    for (let year = currentYear; year >= currentYear - 5; year--) {
      years.push(year);
    }
    return years;
  });

  recentDownloads = signal<Array<{
    request: DownloadRequest;
    progress: DownloadProgress;
  }>>([]);

  constructor() {
    const currentDate = new Date();

    this.downloadForm = this.fb.group({
      year: [currentDate.getFullYear(), Validators.required],
      month: [currentDate.getMonth() + 1, Validators.required],
      format: ['pdf', Validators.required]
    });
  }

  async onDownload(): Promise<void> {
    if (this.downloadForm.invalid) {
      return;
    }

    const formValue = this.downloadForm.value;
    const request: DownloadRequest = {
      year: formValue.year,
      month: formValue.month,
      format: formValue.format
    };

    try {
      // Add to recent downloads with initial state
      this.addToRecentDownloads(request);

      // Start download
      this.downloadService.downloadAndSaveStatement(request).subscribe({
        next: () => {
          console.log('Download completed successfully');
          this.updateDownloadState(request, {
            isDownloading: false,
            progress: 100,
            retryCount: 0
          });
        },
        error: (error) => {
          console.error('Download failed:', error);
          this.updateDownloadState(request, {
            isDownloading: false,
            progress: 0,
            retryCount: 0,
            error: error.message
          });
        }
      });

      // Monitor download progress
      this.monitorDownloadProgress(request);

    } catch (error: any) {
      console.error('Download failed:', error);
    }
  }

  retryDownload(request: DownloadRequest): void {
    // Clear error state and retry
    this.downloadService.clearDownloadState(request);
    this.onDownloadRequest(request);
  }

  downloadAgain(request: DownloadRequest): void {
    this.onDownloadRequest(request);
  }

  cancelDownload(request: DownloadRequest): void {
    // Note: HTTP requests can't be truly cancelled, but we can stop monitoring
    this.updateDownloadState(request, {
      isDownloading: false,
      progress: 0,
      retryCount: 0,
      error: 'Download cancelled'
    });
  }

  private async onDownloadRequest(request: DownloadRequest): Promise<void> {
    try {
      this.addToRecentDownloads(request);

      this.downloadService.downloadAndSaveStatement(request).subscribe({
        next: () => {
          this.updateDownloadState(request, {
            isDownloading: false,
            progress: 100,
            retryCount: 0
          });
        },
        error: (error) => {
          this.updateDownloadState(request, {
            isDownloading: false,
            progress: 0,
            retryCount: 0,
            error: error.message
          });
        }
      });

      this.monitorDownloadProgress(request);

    } catch (error: any) {
      console.error('Download failed:', error);
    }
  }

  private monitorDownloadProgress(request: DownloadRequest): void {
    this.downloadService.getDownloadState$(request).subscribe({
      next: (progress) => {
        this.updateDownloadState(request, progress);
      },
      error: (error) => {
        console.error('Progress monitoring failed:', error);
      }
    });
  }

  private addToRecentDownloads(request: DownloadRequest): void {
    const downloads = this.recentDownloads();

    // Remove existing download with same parameters
    const filteredDownloads = downloads.filter(d =>
      !(d.request.year === request.year &&
        d.request.month === request.month &&
        d.request.format === request.format)
    );

    // Add new download at the beginning
    const newDownloads = [{
      request,
      progress: {
        isDownloading: true,
        progress: 0,
        retryCount: 0
      }
    }, ...filteredDownloads].slice(0, 10); // Keep only last 10 downloads

    this.recentDownloads.set(newDownloads);
  }

  private updateDownloadState(request: DownloadRequest, progress: DownloadProgress): void {
    const downloads = this.recentDownloads();
    const updatedDownloads = downloads.map(d => {
      if (d.request.year === request.year &&
          d.request.month === request.month &&
          d.request.format === request.format) {
        return { ...d, progress };
      }
      return d;
    });

    this.recentDownloads.set(updatedDownloads);
  }

  isAnyDownloadInProgress(): boolean {
    return this.recentDownloads().some(d => d.progress.isDownloading);
  }

  getDownloadDisplayName(request: DownloadRequest): string {
    return `Account Statement - ${this.getMonthName(request.month)} ${request.year}`;
  }

  getMonthName(monthNumber: number): string {
    return this.months()[monthNumber - 1] || 'Unknown';
  }
}
